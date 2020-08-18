using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRageMath;

namespace IngameScript
{
    internal partial class Program : MyGridProgram
    {

        #region mdk preserve

        // CHANGEABLE VARIABLES:

        int speedSetting = 2;                           // 1 = Cinematic, 2 = Classic, 3 = Breakneck
                                                                     // Cinematic: Slower but looks cooler, especially for larger ships.
                                                                     // Classic: Lands at the classic pace.
                                                                     // Breakneck: Still safe, but will land pretty much as quick as it can.
        

        double caution = 0.4;                                             // Between 0 - 0.9. Defines how close to max deceleration the ship will ride.
        bool extra_info = false;                                          // If true, this script will give you more information about what's happening than usual.
        string your_title = "Captain";                                  // How the ship will refer to you.
        bool small_ship_rotate_on_connector = true;       //If enabled, small ships will rotate on the connector to face the saved direction.
        bool large_ship_rotate_on_connector = false;      //If enabled, large ships will rotate on the connector to face the saved direction.
        bool rotate_on_approach = false;                          //If enabled,  the ship will rotate to the saved direction on connector approach.
        double topSpeed = 100;                                         // The top speed the ship will go in m/s.

        bool extra_soft_landing_mode = false;                 // If your ship is hitting your connector too hard, enable this.
        double connector_clearance = 0;                          // If you raise this number (measured in meters), the ship will fly connector_clearance higher before coming down onto the connector.
        double add_acceleration = 0;                                // If your ship is accelerating very slow, or perhaps stopping at a low top speed, try raising this (e.g to 10).


        bool enable_antenna_function = true;                   //If enabled, the ship will try to search for an optional home script. Disable if the antenna functionality is giving you problems.

        bool allow_connector_on_seperate_grid = false; // WARNING: All connectors on your ship must have [dock] in the name if you set this to true! This option allows your connector to not be on the same grid.


        // Waypoint settings:
        double required_waypoint_accuracy = 6;             // how close the ship needs to be to a waypoint to complete it (measured in meters). Do note, closer waypoints are more accurate anyway.
        double waypoints_top_speed = 100;                     // the top speed the ship will go in m/s when it's moving towards waypoints
        bool rotate_during_waypoints = true;                    // if true, the ship will rotate to face each waypoint's direction as it goes along.



        // This code has been minified by Malware's MDK minifier.
        // Find the original source code here:
        // https://github.com/ksqk34/Autodocking-2




        // DO NOT CHANGE BELOW THIS LINE
        // Well you can try...
        private readonly ShipSystemsAnalyzer systemsAnalyzer;
        #endregion
        private const double updatesPerSecond = 10; // Defines how many times the script performes it's calculations per second.

        // Script constants:
        private const double proportionalConstant = 2;
        private const double derivativeConstant = .5;
        private const double timeLimit = 1 / updatesPerSecond;
        private readonly AntennaHandler antennaHandler;
        private readonly List<HomeLocation> homeLocations;
        private readonly double issueDetection = 0;
        private readonly PID pitchPID;
        private readonly PID rollPID;
        private readonly IOHandler shipIOHandler;

        // Script systems:
        

        private readonly ShipSystemsController systemsController;
        private readonly PID yawPID;
        private double anglePitch;


        // Ship vector math variables:
        private double angleRoll;
        private double angleYaw;

        public List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();


        // Script States:
        private string current_argument;

        private double DeltaTimeReal;
        private double DeltaTime;

        private bool errorState;
        private bool hasConnectionToAntenna;
        private bool lastUpdateWasApproach;
        private Vector3D platformVelocity;
        private DateTime previousTime;

        private Vector3D previousVelocity = Vector3D.Zero;
        private string runningIssues = "";

        private double safetyAcceleration = 1;
        private bool scriptEnabled;
        private DateTime scriptStartTime;
        private int current_waypoint_number = 0;

        private string status = "";

        //string persistantText = "";
        private double timeElapsed;
        private double timeElapsedSinceAntennaCheck;
        private double topSpeedUsed = 100;

        // Thanks to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts

        public Program()
        {

            errorState = false;
            Runtime.UpdateFrequency = UpdateFrequency.Once;
            platformVelocity = Vector3D.Zero;
            GridTerminalSystem.GetBlocks(blocks);

            homeLocations = new List<HomeLocation>();

            shipIOHandler = new IOHandler(this);

            if (Storage.Length > 0)
                RetrieveStorage();

            antennaHandler = new AntennaHandler(this);
            systemsAnalyzer = new ShipSystemsAnalyzer(this);
            systemsController = new ShipSystemsController(this);


            pitchPID = new PID(proportionalConstant, 0, derivativeConstant, -10, 10, timeLimit);
            rollPID = new PID(proportionalConstant, 0, derivativeConstant, -10, 10, timeLimit);
            yawPID = new PID(proportionalConstant, 0, derivativeConstant, -10, 10, timeLimit);

            timeElapsed = 0;
            timeElapsedSinceAntennaCheck = 0;
            SafelyExit();
        }

        private void RetrieveStorage()
        {
            //Data 

            var two_halves = Storage.Split('#');
            //if (copy_paste_persistant_memory)
            //    two_halves = Me.CustomData.Split('#');

            var home_location_data = two_halves[0].Split(';');

            foreach (var dataItem in home_location_data)
                if (dataItem.Length > 0)
                {
                    var newLoc = new HomeLocation(dataItem, this);
                    if (newLoc.shipConnector != null) homeLocations.Add(newLoc);
                }
        }

        //Help from Whip.
        private double AlignWithGravity(Waypoint waypoint, bool requireYawControl)
        {
            if (waypoint.RequireRotation)
            {
                var referenceBlock = systemsAnalyzer.currentHomeLocation.shipConnector;


                var referenceOrigin = referenceBlock.GetPosition();
                
                var targetDirection = -waypoint.forward;
                var gravityVecLength = targetDirection.Length();
                if (targetDirection.LengthSquared() == 0)
                {
                    foreach (var thisGyro in systemsAnalyzer.gyros) thisGyro.SetValue("Override", false);
                    return -1;
                }

                //var block_WorldMatrix = referenceBlock.WorldMatrix;
                var block_WorldMatrix = Matrix.CreateWorld(referenceOrigin,
                    referenceBlock.WorldMatrix.Up, //referenceBlock.WorldMatrix.Forward,
                    -referenceBlock.WorldMatrix.Forward //referenceBlock.WorldMatrix.Up
                );

                var referenceForward = block_WorldMatrix.Forward;
                var referenceLeft = block_WorldMatrix.Left;
                var referenceUp = block_WorldMatrix.Up;

                anglePitch =
                    Math.Acos(MathHelper.Clamp(targetDirection.Dot(referenceForward) / gravityVecLength, -1, 1)) -
                    Math.PI / 2;
                Vector3D planetRelativeLeftVec = referenceForward.Cross(targetDirection);
                angleRoll = PID.VectorAngleBetween(referenceLeft, planetRelativeLeftVec);
                angleRoll *= PID.VectorCompareDirection(PID.VectorProjection(referenceLeft, targetDirection),
                    targetDirection); //ccw is positive 
                if (requireYawControl)
                    //angleYaw = 0;
                    angleYaw = Math.Acos(MathHelper.Clamp(waypoint.auxilleryDirection.Dot(referenceLeft), -1, 1)) - Math.PI / 2;
                else
                    angleYaw = 0;
                //shipIOHandler.Echo("Angle Yaw: " + IOHandler.RoundToSignificantDigits(angleYaw, 2).ToString());


                anglePitch *= -1;
                angleRoll *= -1;


                //shipIOHandler.Echo("Pitch angle: " + Math.Round((anglePitch / Math.PI * 180), 2).ToString() + " deg");
                //shipIOHandler.Echo("Roll angle: " + Math.Round((angleRoll / Math.PI * 180), 2).ToString() + " deg");

                //double rawDevAngle = Math.Acos(MathHelper.Clamp(targetDirection.Dot(referenceForward) / targetDirection.Length() * 180 / Math.PI, -1, 1));
                var rawDevAngle = Math.Acos(MathHelper.Clamp(targetDirection.Dot(referenceForward), -1, 1)) * 180 /
                                  Math.PI;
                rawDevAngle -= 90;

                //shipIOHandler.Echo("Angle: " + rawDevAngle.ToString());


                var rollSpeed = rollPID.Control(angleRoll);
                var pitchSpeed = pitchPID.Control(anglePitch);
                double yawSpeed = 0;
                if (requireYawControl) yawSpeed = yawPID.Control(angleYaw);

                //---Set appropriate gyro override  
                if (!errorState)
                    //do gyros
                    systemsController.ApplyGyroOverride(pitchSpeed, yawSpeed, -rollSpeed, systemsAnalyzer.gyros,
                        block_WorldMatrix);
                return rawDevAngle;
            }

            return -1;
        }
        private double AlignWithWaypoint(Waypoint waypoint)
        {

            MatrixD stationConnectorWorldMatrix = Matrix.CreateWorld(systemsAnalyzer.currentHomeLocation.stationConnectorPosition, systemsAnalyzer.currentHomeLocation.stationConnectorForward,
                (-systemsAnalyzer.currentHomeLocation.stationConnectorLeft).Cross(systemsAnalyzer.currentHomeLocation.stationConnectorForward));

            var referenceGrid = Me.CubeGrid;

            Vector3D waypointForward = HomeLocation.localDirectionToWorldDirection(waypoint.forward, systemsAnalyzer.currentHomeLocation);
            Vector3D waypointRight = HomeLocation.localDirectionToWorldDirection(waypoint.auxilleryDirection, systemsAnalyzer.currentHomeLocation);


            var targetDirection = waypointForward;
            


            var referenceOrigin = referenceGrid.GetPosition();



            var block_WorldMatrix = Matrix.CreateWorld(referenceOrigin,
                referenceGrid.WorldMatrix.Up, //referenceBlock.WorldMatrix.Forward,
                -referenceGrid.WorldMatrix.Forward //referenceBlock.WorldMatrix.Up
            );

            var referenceForward = block_WorldMatrix.Forward;
            var referenceLeft = block_WorldMatrix.Left;
            var referenceUp = block_WorldMatrix.Up;

            anglePitch = Math.Acos(MathHelper.Clamp(targetDirection.Dot(referenceForward), -1, 1)) - Math.PI / 2;
            //anglePitch *= PID.VectorCompareDirection(targetDirection, referenceForward);


            Vector3D relativeLeftVec = referenceForward.Cross(targetDirection);
            angleRoll = PID.VectorAngleBetween(referenceLeft, relativeLeftVec);
            angleRoll *= PID.VectorCompareDirection(PID.VectorProjection(referenceLeft, targetDirection),
                targetDirection); //ccw is positive 
                                  //angleRoll *= PID.VectorCompareDirection(PID.VectorProjection(referenceLeft, targetDirection),
                                  //    targetDirection); //ccw is positive 

                                  Vector3D waypointUp = (-waypointRight).Cross(waypointForward);
            angleYaw = Math.Acos(MathHelper.Clamp((-waypointUp).Dot(referenceLeft), -1, 1)) - Math.PI / 2;
            //angleYaw *= PID.VectorCompareDirection(PID.VectorProjection(referenceLeft, targetDirection), targetDirection);

            anglePitch *= -1;
            angleRoll *= -1;

            //shipIOHandler.Echo("Pitch angle: " + Math.Round((anglePitch / Math.PI * 180), 2).ToString() + " deg");
            //shipIOHandler.Echo("Roll angle: " + Math.Round((angleRoll / Math.PI * 180), 2).ToString() + " deg");
            //shipIOHandler.Echo("Yaw angle: " + Math.Round((angleYaw / Math.PI * 180), 2).ToString() + " deg");

            //double rawDevAngle = Math.Acos(MathHelper.Clamp(targetDirection.Dot(referenceForward) / targetDirection.Length() * 180 / Math.PI, -1, 1));
            var rawDevAngle = Math.Acos(MathHelper.Clamp(targetDirection.Dot(referenceForward), -1, 1)) * 180 /
                                Math.PI;
            rawDevAngle -= 90;

            //shipIOHandler.Echo("Angle: " + rawDevAngle.ToString());


            var rollSpeed = rollPID.Control(angleRoll) * 1;
            var pitchSpeed = pitchPID.Control(anglePitch) * 1;
            double yawSpeed = yawPID.Control(angleYaw) * 1;

            //---Set appropriate gyro override  
            if (!errorState)
                //do gyros
                systemsController.ApplyGyroOverride(pitchSpeed, yawSpeed, -rollSpeed, systemsAnalyzer.gyros,
                    block_WorldMatrix);
            return rawDevAngle;

        }

        public void Save()
        {
            produceDataString();
        }

        public void produceDataString()
        {
            Storage = "";
            //if (copy_paste_persistant_memory)
            //    Me.CustomData = "";
            //Data 
            foreach (var homeLocation in homeLocations)
            {
                AppendToStorage(homeLocation.ProduceSaveData() + ";");
                //Storage += homeLocation.ProduceSaveData() + ";";
                //if (copy_paste_persistant_memory)
                //    Me.CustomData += homeLocation.ProduceSaveData() + ";";
            }
            //Storage += "#";
            AppendToStorage("#");
        }

        public void AppendToStorage(string data)
        {
            Storage += data;
            //if (copy_paste_persistant_memory)
            //    Me.CustomData += data;
        }

        /// <summary>Begins the ship docking sequence. Requires (Will require) a HomeLocation and argument.</summary>
        /// <param name="beginConnector"></param>
        /// <param name="argument"></param>
        /// /// <param name="connectorOverride">override the ship connector used to dock</param>
        public void Begin(string argument, IMyShipConnector connectorOverride = null) // WARNING, NEED TO ADD HOME LOCATION IN FUTURE INSTEAD
        {
            systemsAnalyzer.currentHomeLocation = FindHomeLocation(argument);
            if (systemsAnalyzer.currentHomeLocation != null)
            {
                if (connectorOverride != null)
                {
                    systemsAnalyzer.currentHomeLocation.shipConnector = connectorOverride;
                    systemsAnalyzer.currentHomeLocation.shipConnectorID = connectorOverride.EntityId;
                }

                Me.CustomData = systemsAnalyzer.currentHomeLocation.shipConnector.EntityId.ToString();

                systemsAnalyzer.currentHomeLocation.stationVelocity = Vector3D.Zero;
                systemsAnalyzer.currentHomeLocation.stationAcceleration = Vector3D.Zero;
                systemsAnalyzer.currentHomeLocation.stationAngularVelocity = Vector3D.Zero;
                current_argument = argument;
                scriptEnabled = true;
                hasConnectionToAntenna = false;
                lastUpdateWasApproach = false;
                current_waypoint_number = 0;
                runningIssues = "";
                safetyAcceleration = 1;
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
                scriptStartTime = DateTime.Now;
                previousTime = DateTime.Now;
                previousVelocity = systemsAnalyzer.cockpit.GetShipVelocities().LinearVelocity;

                if (enable_antenna_function) antennaHandler.SendPositionUpdateRequest(1);
            }
            else
            {
                SafelyExit();
            }
        }

        public string updateHomeLocation(string argument, IMyShipConnector my_connected_connector)
        {
            var station_connector = my_connected_connector.OtherConnector;
            if (station_connector == null)
            {
                shipIOHandler.Error(
                    "\nSomething went wrong when finding the connector.\nMaybe you have multiple connectors on the go, " +
                    your_title + "?");
                return "";
            }

            // We create a new home location, so that it can be compared with all the others.
            var newHomeLocation = new HomeLocation(argument, my_connected_connector, station_connector);
            var HomeLocationIndex = homeLocations.LastIndexOf(newHomeLocation);
            if (HomeLocationIndex != -1)
            {
                // Docking location that was just created, already exists.
                if (extra_info)
                    shipIOHandler.Echo("- Docking location already Exists!\n- Adding argument.");
                if (!homeLocations[HomeLocationIndex].arguments.Contains(argument))
                {
                    if (extra_info)
                        shipIOHandler.Echo("Other arguments associated: " +
                                           shipIOHandler.GetHomeLocationArguments(homeLocations[HomeLocationIndex]));
                    homeLocations[HomeLocationIndex].arguments.Add(argument);
                    if (extra_info)
                        shipIOHandler.Echo("- New argument added.");
                }
                else if (extra_info)
                {
                    shipIOHandler.Echo("- Argument already in!");
                    if (extra_info)
                        shipIOHandler.Echo("All arguments associated: " +
                                           shipIOHandler.GetHomeLocationArguments(homeLocations[HomeLocationIndex]));
                }

                homeLocations[HomeLocationIndex].UpdateData(my_connected_connector, station_connector);
            }
            else
            {
                homeLocations.Add(newHomeLocation);
                if (extra_info)
                    shipIOHandler.Echo("- Added new docking location.");
            }

            //Check if any homelocations had that argument before, if so, remove it.
            var amountFound = 0;
            var toDelete = new List<HomeLocation>();
            foreach (var currentHomeLocation in homeLocations)
                if (!currentHomeLocation.Equals(newHomeLocation))
                    if (currentHomeLocation.arguments.Contains(argument))
                    {
                        amountFound += 1;

                        currentHomeLocation.arguments.Remove(argument);
                        if (currentHomeLocation.arguments.Count == 0) toDelete.Add(currentHomeLocation);
                    }

            while (toDelete.Count > 0)
            {
                homeLocations.Remove(toDelete[0]);
                toDelete.RemoveAt(0);
            }

            if (extra_info)
            {
                if (amountFound == 1)
                    shipIOHandler.Echo("- Found 1 other association with that argument. Removed this other.");
                else if (amountFound > 1)
                    shipIOHandler.Echo("- Found " + amountFound +
                                       " other associations with that argument. Removed these others.");
            }

            if (argument == "")
            {
                if (!extra_info)
                    return "SAVED\nSaved docking location as no argument, " + your_title + ".";
                return "Saved docking location as no argument, " + your_title + ".";
            }

            if (!extra_info)
                return "SAVED\nSaved docking location as " + argument + ", " + your_title + ".";
            return "Saved docking location as " + argument + ", " + your_title + ".";
        }


        private bool recording = false;
        private bool waiting_for_arg = false;
        private string recording_arg = "";
        private List<Vector3D> waypoints_positions;
        private List<Vector3D> waypoints_forwards;
        private List<Vector3D> waypoints_rights;
        private List<double> waypoints_speeds;
        private List<bool> waypoints_rotates;

        public void beginRecordingSetup()
        {
            recording = true;
            waiting_for_arg = true;
            SafelyExit();
            shipIOHandler.Echo("RECORDING MODE\nPlease enter an argument that\nwill be associated with these waypoints then press Run, " + your_title + ".\n\nTo cancel, press Recompile.");
        }

        public void beginRecordingWaypoints(string arg)
        {
            if (arg.ToLower().Trim() == "record")
            {
                shipIOHandler.Echo("RECORDING MODE\nPlease choose an argument\nother than record, then press Run, " + your_title + ".\n\nTo cancel, press Recompile.");
            }
            else
            {
                recording = true;
                waiting_for_arg = false;
                recording_arg = arg;
                current_waypoint_number = 0;
                waypoints_positions = new List<Vector3D>();
                waypoints_forwards = new List<Vector3D>();
                waypoints_rights = new List<Vector3D>();
                waypoints_speeds = new List<double>();
                waypoints_rotates = new List<bool>();
                shipIOHandler.WaypointEcho(recording_arg, current_waypoint_number, "");
            }
        }

        public double accuracyFromDistance(Vector3D start_pos, Vector3D end_pos)
        {
            double dist = Vector3D.Distance(start_pos, end_pos) / 4;
            if (dist < 0.2)
            {
                dist = 0.2;
            }
            return dist;
        }
        public void recordWaypoint(IMyShipConnector connectedConnector, string argument)
        {
            if (connectedConnector == null)
            {
                waypoints_positions.Add(Me.CubeGrid.GetPosition());
                waypoints_forwards.Add(Me.CubeGrid.WorldMatrix.Forward);
                waypoints_rights.Add(Me.CubeGrid.WorldMatrix.Right);

                double speed = -1;
                bool waypoints_rotate = true;
                if (argument.Trim().Length > 1)
                {
                    if (argument.Trim()[0] == '!')
                    {
                        string second_part = argument.Remove(0, 1);
                        double speed_num;
                        bool result = double.TryParse(second_part, out speed_num);
                        if (result)
                        {
                            speed = speed_num;
                            
                        }
                    }
                }
                if (argument.ToLower().Trim().Contains("no spin") || argument.ToLower().Trim().Contains("nospin") || argument.ToLower().Trim().Contains("!nospin"))
                {
                    waypoints_rotate = false;
                }
                waypoints_speeds.Add(speed);
                waypoints_rotates.Add(waypoints_rotate);

                string extra_output = "";
                if (speed > 0)
                {
                    extra_output = "\nRecorded speed: " + speed;
                }
                if (!waypoints_rotate)
                {
                    extra_output += "\nRecorded no rotate";
                }
                current_waypoint_number += 1;
                shipIOHandler.WaypointEcho(recording_arg, current_waypoint_number, extra_output);


            }
            else
            {
                shipIOHandler.Echo("FINISHED RECORDING");
                if (current_waypoint_number == 0)
                {
                    shipIOHandler.Echo("No waypoints recorded.");
                    var result = updateHomeLocation(recording_arg, connectedConnector);
                }
                else
                {
                    
                    recording = false;
                    waiting_for_arg = false;

                    var result = updateHomeLocation(recording_arg, connectedConnector);
                    HomeLocation currentHomeLocation = FindHomeLocation(recording_arg);

                    List<Waypoint> landing_sequence = new List<Waypoint>();

                    Vector3D stationConnectorPos = currentHomeLocation.stationConnectorPosition;
                    Vector3D stationConnectorForward = currentHomeLocation.stationConnectorForward;
                    Vector3D stationConnectorLeft = currentHomeLocation.stationConnectorLeft;

                    MatrixD stationConnectorWorldMatrix = Matrix.CreateWorld(stationConnectorPos, stationConnectorForward,
                        (-stationConnectorLeft).Cross(stationConnectorForward));

                    Vector3D last_world_pos = Vector3D.Zero;
                    for (int waypointIndex = 0; waypointIndex < current_waypoint_number; waypointIndex++)
                    {
                        Vector3D waypointGlobalPosition = waypoints_positions[waypointIndex];
                        Vector3D waypointLocalPositionToStation = HomeLocation.worldPositionToLocalPosition(waypointGlobalPosition, stationConnectorWorldMatrix);

                        double calculated_accuracy = required_waypoint_accuracy;
                        if (waypointIndex > 0)
                        {
                            double last_accuracy = accuracyFromDistance(waypointGlobalPosition, last_world_pos);
                            calculated_accuracy = Math.Min(last_accuracy, required_waypoint_accuracy);
                        }
                        if (waypointIndex == current_waypoint_number - 1)
                        {
                            double last_accuracy = accuracyFromDistance(waypointGlobalPosition, stationConnectorPos) * 0.7;
                            calculated_accuracy = Math.Min(last_accuracy, calculated_accuracy);
                        }
                        


                        Vector3D gridForwardToLocal =
                            HomeLocation.worldDirectionToLocalDirection(waypoints_forwards[waypointIndex],
                                stationConnectorWorldMatrix);

                        Vector3D gridRightToLocal =
                            HomeLocation.worldDirectionToLocalDirection(waypoints_rights[waypointIndex],
                                stationConnectorWorldMatrix);

                        double waypoint_speed = waypoints_speeds[waypointIndex];
                        if (waypoint_speed < 0)
                        {
                            waypoint_speed = waypoints_top_speed;
                        }

                        Waypoint newWaypoint = new Waypoint(waypointLocalPositionToStation, gridForwardToLocal,
                            gridRightToLocal)
                        {
                            WaypointIsLocal = true,
                            maximumAcceleration = 15,
                            RequireRotation = waypoints_rotates[waypointIndex],
                            waypoint_completion_accuracy = calculated_accuracy,
                            top_speed = waypoint_speed
                        };
                        last_world_pos = waypointGlobalPosition;
                        landing_sequence.Add(newWaypoint);
                    }

                    if (currentHomeLocation.landingSequences.ContainsKey(recording_arg))
                    {
                        shipIOHandler.Echo("Overwriting existing waypoints.");
                    }

                    currentHomeLocation.landingSequences[recording_arg] = landing_sequence;

                    
                    shipIOHandler.Echo("Recorded " + (current_waypoint_number + 1).ToString() + " waypoints to argument: " + IOHandler.ConvertArg(recording_arg));
                    current_waypoint_number = 0;
                }
            }
            
        }

        public string checkForClear(string argument)
        {

            string[] split = argument.Split(' ');

            if (split.Length > 1)
            {
                if (split[0].ToLower() == "clear")
                {
                    return argument.Remove(0, 6);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }



        public void ClearMemoryLocation(string argument)
        {
            //Check if any homelocations had that argument before, if so, remove it.
            bool found_arg = false;
            var toDelete = new List<HomeLocation>();
            foreach (var currentHomeLocation in homeLocations)
                if (currentHomeLocation.arguments.Contains(argument)) {
                        currentHomeLocation.arguments.Remove(argument);
                        if (currentHomeLocation.landingSequences.ContainsKey(argument))
                        {
                            currentHomeLocation.landingSequences.Remove(argument);
                        }
                        found_arg = true;
                        if (currentHomeLocation.arguments.Count == 0) toDelete.Add(currentHomeLocation);
                }

            while (toDelete.Count > 0)
            {
                homeLocations.Remove(toDelete[0]);
                toDelete.RemoveAt(0);
            }

            if (found_arg)
            {
                shipIOHandler.Echo("CLEARED\nThe argument: " + IOHandler.ConvertArg(argument) + "\nhas been cleared from memory.");
            }
            else
            {
                shipIOHandler.Echo("WARNING\nThe argument: " + IOHandler.ConvertArg(argument) + "\nwasn't found in memory.");
            }
            
        }

        public string ProduceDataOutputString()
        {
            string o_string = "";
            foreach (var homeLocation in homeLocations)
            {
                o_string += homeLocation.ProduceUserFriendlyData() + "\n";

            }

            if (o_string.Length > 1)
            {
                o_string = o_string.Substring(0, o_string.Length - 1);
            }

            return o_string;
        }

        public IMyShipConnector CheckForConnectorOverride(ref string argument)
        {
            if (argument.Contains("!"))
            {
                string[] arg_split = argument.Trim().Split('!');
                if (arg_split.Length > 1)
                {
                    string afterSeperator = arg_split[1].TrimEnd();
                    if (afterSeperator.Length > 0)
                    {
                        long ID_extracted;
                        bool success = long.TryParse(afterSeperator, out ID_extracted);
                        if (success)
                        {
                            IMyShipConnector new_connector = (IMyShipConnector)GridTerminalSystem.GetBlockWithId(ID_extracted);
                            if (new_connector != null)
                            {
                                
                                string resultant_arg = "";
                                string[] raw_split = argument.Split('!');
                                for (int i = 0; i < raw_split.Length - 1; i++)
                                {
                                    resultant_arg += raw_split[i];
                                }

                                resultant_arg = resultant_arg.TrimEnd();

                                argument = resultant_arg;
                                return new_connector;
                            }
                        }
                    }
                }
            }

            return null;
        }

        public bool checkForReadonly(ref string argument)
        {
            if(argument.Length > 1)
            {
                if (argument[0] == '!' && argument.Contains(" "))
                {
                    string[] arg_split = argument.Split(' ');
                    if(arg_split.Length > 1)
                    {
                        if(arg_split[0].ToLower() == "!readonly")
                        {
                            argument = argument.Remove(0, 10);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & (UpdateType.Update1 | UpdateType.Once | UpdateType.IGC)) == 0)

            {
                // Script is activated by pressing "Run"
                if (errorState)
                {
                    if (argument.ToLower().Trim() == "[data_output_request]")
                    {
                        Me.CustomData = ProduceDataOutputString();
                    }
                    // If an error has happened
                    errorState = false;
                    systemsAnalyzer.GatherBasicData();

                }

                if (!errorState)
                {
                    // script was activated and there was no error so far.

                    if (!recording)
                    {


                        var my_connected_connector = systemsAnalyzer.FindMyConnectedConnector();

                        var clear_command = checkForClear(argument);
                        if (argument.ToLower().Trim() == "record")
                        {
                            if (my_connected_connector == null)
                            {
                                beginRecordingSetup();
                            }
                            else
                            {
                                shipIOHandler.Echo("WARNING\nPlease make sure you are not connected\nto a home connector before recording, " + your_title + ".");
                            }
                        }

                        else if (argument.ToLower().Trim() == "[data_output_request]")
                        {
                            Me.CustomData = ProduceDataOutputString();
                        }
                        else if (clear_command != null)
                        {
                            ClearMemoryLocation(clear_command);
                        }
                        else
                        {
                            var connectorOverride = CheckForConnectorOverride(ref argument);
                            bool user_wants_readonly = checkForReadonly(ref argument);

                            if (user_wants_readonly && my_connected_connector != null)
                            {

                                    if (my_connected_connector.Status == MyShipConnectorStatus.Connectable)
                                    {
                                        my_connected_connector = null;
                                    }
                                
                            }

                            if (my_connected_connector == null)
                            {
                                if (scriptEnabled && argument == current_argument)
                                {
                                    // Script was already running, and using current argument, therefore this is a stopping order.
                                    shipIOHandler.Echo("STOPPED\nAwaiting orders, " + your_title + ".");
                                    SafelyExit();
                                }
                                else
                                {
                                    // Request to dock initialized.
                                    if (connectorOverride != null)
                                    {
                                        Begin(argument, connectorOverride);
                                    }
                                    else
                                    {
                                        Begin(argument);
                                    }
                                    
                                }
                            }
                            else
                            {
                                if (!user_wants_readonly)
                                {
                                    if (connectorOverride != null)
                                    {
                                        my_connected_connector = connectorOverride;
                                    }

                                    var result = updateHomeLocation(argument, my_connected_connector);
                                    shipIOHandler.Echo(result);
                                    //shipIOHandler.Echo("\nThis location also has\nother arguments associated:");
                                    SafelyExit();
                                }
                                else
                                {
                                    shipIOHandler.Echo("OVERRIDDEN\nShip save has been overriden\ndue to the !readonly command.");
                                }
                            }
                        }
                    }
                    else
                    {
                        // In recording mode and user pressed "Run"

                        if (waiting_for_arg)
                        {
                            beginRecordingWaypoints(argument);
                        }
                        else
                        {
                            var connectorOverride = CheckForConnectorOverride(ref argument);
                            var my_connected_connector = systemsAnalyzer.FindMyConnectedConnector();
                            if (my_connected_connector != null && connectorOverride != null)
                            {
                                my_connected_connector = connectorOverride;
                            }
                            recordWaypoint(my_connected_connector, argument);
                        }
                        
                    }
                }

                shipIOHandler.EchoFinish();
            }

            // Script docking is running:
            if (scriptEnabled && !errorState)
            {
                timeElapsed += Runtime.TimeSinceLastRun.TotalSeconds;
                if (timeElapsed >= timeLimit)
                {
                    systemsAnalyzer.CheckForMassChange();

                    if (hasConnectionToAntenna && enable_antenna_function) antennaHandler.SendPositionUpdateRequest(1);

                    // Do docking sequence:
                    DockingSequenceFrameUpdate();
                    timeElapsed = 0;
                }
            }

            if (enable_antenna_function && scriptEnabled && !errorState && !hasConnectionToAntenna)
            {
                //We need to search for a docking script home antenna.
                timeElapsedSinceAntennaCheck += Runtime.TimeSinceLastRun.TotalSeconds;
                if (timeElapsedSinceAntennaCheck >= 1)
                {
                    antennaHandler.SendPositionUpdateRequest(1);
                    timeElapsedSinceAntennaCheck = 0;
                }
            }


            if ((updateSource & UpdateType.IGC) != 0 && enable_antenna_function)
                //shipIOHandler.Clear();

                // Has recieved a message
                antennaHandler.HandleMessage();
            //shipIOHandler.EchoFinish();
        }

        //public void ScriptMain(string argum)
        //{

        //}

        private HomeLocation FindHomeLocation(string argument)
        {
            var amountFound = 0;
            HomeLocation resultantHomeLocation = null;
            foreach (var currentHomeLocation in homeLocations)
                if (currentHomeLocation.arguments.Contains(argument))
                {
                    amountFound += 1;
                    if (resultantHomeLocation == null) resultantHomeLocation = currentHomeLocation;
                }

            if (amountFound > 1)
                shipIOHandler.Echo("Minor Warning:\nThere are " + amountFound +
                                   " places\nthat argument is associated with!\nPicking first one found, " +
                                   your_title + ".");
            else if (amountFound == 0)
                shipIOHandler.Echo("WARNING:\nNo docking location found with that argument, " + your_title +
                                   ".\nPlease dock to a connector and press 'Run' with your argument\nto save it as a docking location.");
            return resultantHomeLocation;
        }

        /// <summary>Equivalent to "Update()" from Unity but specifically for the docking sequence.</summary>
        private void DockingSequenceFrameUpdate()
        {
            if (systemsAnalyzer.currentHomeLocation.landingSequences.ContainsKey(current_argument))
            {
                List<Waypoint> landing_sequence =
                    systemsAnalyzer.currentHomeLocation.landingSequences[current_argument];

                if (current_waypoint_number >= landing_sequence.Count)
                {
                    // We have completed all waypoints so land straight to the connector.

                    AutoLandToConnector(true);

                }
                else
                {
                    // There are waypoints left to complete

                    Waypoint currentWaypoint = landing_sequence[current_waypoint_number];
                    Waypoint nextWaypoint = null;
                    if (current_waypoint_number < landing_sequence.Count - 1)
                    {
                        nextWaypoint = landing_sequence[current_waypoint_number + 1];
                    }

                    

                    double dist_to_waypoint = AutoFollowWaypoint(currentWaypoint, nextWaypoint);

                    double accuracy = Math.Min(required_waypoint_accuracy,
                        currentWaypoint.waypoint_completion_accuracy);

                    if (dist_to_waypoint < accuracy)
                    {
                        // We've reached the latest waypoint.
                        current_waypoint_number += 1;
                    }

                }
            }
            else
            {
                // There are no waypoints for this location.
                AutoLandToConnector(false);
            }
        }

        /// <summary>
        /// Let the ship plot the waypoints to the connector. This occurres when there are no preset waypoints.
        /// </summary>
        /// <param name="only_last_landing">If true, the ship will simply land straight to the connector, not plot a path first.</param>
        private void AutoLandToConnector(bool only_last_landing)
        {
            var dontRotateOnConnector = !small_ship_rotate_on_connector && !systemsAnalyzer.isLargeShip ||
                                        !large_ship_rotate_on_connector && systemsAnalyzer.isLargeShip;


            if (systemsAnalyzer.currentHomeLocation.shipConnector.Status == MyShipConnectorStatus.Connected ||
                systemsAnalyzer.currentHomeLocation.shipConnector.Status == MyShipConnectorStatus.Connectable &&
                dontRotateOnConnector)
            {
                // If it's ready to dock, and we aren't rotating on the connector.
                ConnectAndDock();
            }
            else
            {
                shipIOHandler.DockingSequenceStartMessage(current_argument);

                if (systemsAnalyzer.basicDataGatherRequired)
                {
                    systemsAnalyzer.GatherBasicData();
                    systemsAnalyzer.basicDataGatherRequired = false;
                }

                if (errorState == false)
                {
                    double sideways_dist_needed_to_land = 3;
                    var rotate_on_connector_accuracy = 0.015; // Radians
                    double height_needed_for_connector = 5;

                    if (speedSetting == 1)
                    {
                        height_needed_for_connector = 7;

                        topSpeedUsed = 10;
                    }
                    else if (speedSetting == 3)
                    {
                        height_needed_for_connector = 4;
                        topSpeedUsed = topSpeed;
                        rotate_on_connector_accuracy = 0.03;
                    }
                    else
                    {
                        height_needed_for_connector = 6;
                        topSpeedUsed = topSpeed;
                    }
                    if (extra_soft_landing_mode) height_needed_for_connector = 8;
                    height_needed_for_connector += connector_clearance;

                    if (only_last_landing) height_needed_for_connector = 0;

                    if (topSpeedUsed > topSpeed) topSpeedUsed = topSpeed;
                    if (systemsAnalyzer.currentHomeLocation.stationVelocity.Length() > 5)
                    {
                        rotate_on_connector_accuracy = 0.035;
                        sideways_dist_needed_to_land = 5;
                    }

                    var speedDampener = 1 - (systemsAnalyzer.currentHomeLocation.stationVelocity.Length() / (100 * 0.2)); //
                    //speedDampener = 1;
                    var ConnectorLocation = systemsAnalyzer.currentHomeLocation.stationConnectorPosition +
                                            DeltaTimeReal * systemsAnalyzer.currentHomeLocation.stationVelocity * // CHANGED
                                            speedDampener;
                    var ConnectorDirection = systemsAnalyzer.currentHomeLocation.stationConnectorForward;
                    var ConnectorUp = systemsAnalyzer.currentHomeLocation.stationConnectorUpGlobal;
                    var target_position = ConnectorLocation + ConnectorDirection * height_needed_for_connector;
                    var current_position = systemsAnalyzer.currentHomeLocation.shipConnector.GetPosition();

                    //shipIOHandler.Echo("Loc: " + ConnectorLocation.ToString());
                    //shipIOHandler.Echo("auxilleryDirection: " + systemsAnalyzer.currentHomeLocation.stationConnectorUpGlobal.ToString());
                    //shipIOHandler.Echo("vel: " + systemsAnalyzer.currentHomeLocation.stationVelocity.ToString());
                    //shipIOHandler.Echo("acc: " + systemsAnalyzer.currentHomeLocation.stationAcceleration.ToString());

                    var point_in_sequence = "Starting...";



                    var aboveConnectorWaypoint = new Waypoint(target_position, ConnectorDirection, ConnectorUp);

                    // Constantly ensure alignment
                    double direction_accuracy;
                    var connectedLate = false;
                    if (!dontRotateOnConnector && systemsAnalyzer.currentHomeLocation.shipConnector.Status ==
                        MyShipConnectorStatus.Connectable)
                    {
                        direction_accuracy = AlignWithGravity(aboveConnectorWaypoint, true);
                        if (Math.Abs(angleYaw) < rotate_on_connector_accuracy)
                        {
                            ConnectAndDock();
                            connectedLate = true;
                        }
                    }
                    else
                    {
                        var yaw_rotate = false;
                        if (rotate_on_approach && lastUpdateWasApproach) yaw_rotate = true;
                        direction_accuracy = AlignWithGravity(aboveConnectorWaypoint, yaw_rotate);
                    }

                    lastUpdateWasApproach = false;
                    if (!connectedLate)
                    {
                        if (Math.Abs(direction_accuracy) < 15)
                        {

                            // Test if ship is behind the station connector:
                            var pointOnConnectorAxis = PID.NearestPointOnLine(ConnectorLocation, ConnectorDirection,
                                current_position);
                            var heightDifference = pointOnConnectorAxis - ConnectorLocation;
                            var signedHeightDistanceToConnector =
                                ConnectorDirection.Dot(Vector3D.Normalize(heightDifference)) *
                                heightDifference.Length();
                            var sidewaysDistance = (current_position - pointOnConnectorAxis).Length();


                            if (sidewaysDistance > sideways_dist_needed_to_land && signedHeightDistanceToConnector <
                                height_needed_for_connector * 0.9 && !only_last_landing)
                            {
                                // The ship is behind the connector, so it needs to fly auxilleryDirection to it so that it is on the correct side at least.
                                // Only then can it attempt to land.
                                const double overshoot = 2;
                                var SomewhereOnCorrectSide =
                                    new Waypoint(
                                        current_position + ConnectorDirection *
                                        (-signedHeightDistanceToConnector + overshoot + height_needed_for_connector),
                                        ConnectorDirection, ConnectorUp);
                                SomewhereOnCorrectSide.maximumAcceleration = 20;
                                SomewhereOnCorrectSide.required_accuracy = 0.8;

                                if (speedSetting == 1)
                                    aboveConnectorWaypoint.maximumAcceleration = 8;
                                else if (speedSetting == 3)
                                    aboveConnectorWaypoint.maximumAcceleration = 20;
                                else
                                    aboveConnectorWaypoint.maximumAcceleration = 10;


                                MoveToWaypoint(SomewhereOnCorrectSide);
                                point_in_sequence = "Behind target, moving to be in front";
                            }
                            else if (sidewaysDistance > sideways_dist_needed_to_land && !only_last_landing)
                            {
                                if (speedSetting == 1)
                                    aboveConnectorWaypoint.maximumAcceleration = 5;
                                else if (speedSetting == 3)
                                    aboveConnectorWaypoint.maximumAcceleration = 15;
                                else
                                    aboveConnectorWaypoint.maximumAcceleration = 5;


                                MoveToWaypoint(aboveConnectorWaypoint);
                                point_in_sequence = "Moving toward connector";
                            }
                            else
                            {
                                var connectorHeight = systemsAnalyzer.currentHomeLocation.stationConnectorSize +
                                                      ShipSystemsAnalyzer.GetRadiusOfConnector(systemsAnalyzer
                                                          .currentHomeLocation.shipConnector);
                                var DockedToConnector =
                                    new Waypoint(ConnectorLocation + ConnectorDirection * connectorHeight,
                                        ConnectorDirection, ConnectorUp);
                                DockedToConnector.maximumAcceleration = 3;

                                if (speedSetting == 1)
                                    aboveConnectorWaypoint.maximumAcceleration = 1;
                                else if (speedSetting == 3)
                                    aboveConnectorWaypoint.maximumAcceleration = 3;
                                else
                                    aboveConnectorWaypoint.maximumAcceleration = 1;
                                if (extra_soft_landing_mode) topSpeedUsed = 2;

                                var acc = MoveToWaypoint(DockedToConnector);
                                point_in_sequence = "landing on connector";
                                lastUpdateWasApproach = true;
                            }
                        }
                        else
                        {
                            status = "Rotating";
                            point_in_sequence = "Rotating to connector";
                        }

                        if (extra_info)
                        {
                            shipIOHandler.Echo("Status: " + status);
                            shipIOHandler.Echo("Place in sequence: " + point_in_sequence);
                        }

                        var elapsed = DateTime.Now - scriptStartTime;
                        shipIOHandler.Echo("\nTime elapsed: " + elapsed.Seconds + "." +
                                           elapsed.Milliseconds.ToString().Substring(0, 1));
                        shipIOHandler.EchoFinish();
                    }
                }
            }
        }



        private double AutoFollowWaypoint(Waypoint currentWaypoint, Waypoint nextWaypoint)
        {
            shipIOHandler.DockingSequenceStartMessage(current_argument);

            if (systemsAnalyzer.basicDataGatherRequired)
            {
                systemsAnalyzer.GatherBasicData();
                systemsAnalyzer.basicDataGatherRequired = false;
            }

            if (errorState == true) return 0;

            #region MotionSettings

            // Motion settings

            
            if (speedSetting == 1)
            {
                topSpeedUsed = 10;
            }
            else if (speedSetting == 3)
            {
                topSpeedUsed = currentWaypoint.top_speed;
            }
            else
            {
                topSpeedUsed = currentWaypoint.top_speed;
            }
            if (topSpeedUsed > currentWaypoint.top_speed) topSpeedUsed = currentWaypoint.top_speed;


            #endregion


            //var speedDampener = 1 - systemsAnalyzer.currentHomeLocation.stationVelocity.Length() / 100 * 0.2;
            //var WaypointLocation = currentWaypoint.position + (DeltaTimeReal * systemsAnalyzer.currentHomeLocation.stationVelocity * speedDampener);
            //var current_position = Me.CubeGrid.GetPosition();


            //var point_in_sequence = "Starting...";

            // Constantly ensure alignment
            if (rotate_during_waypoints && currentWaypoint.RequireRotation)
            {
                AlignWithWaypoint(currentWaypoint);
            }
            

            if (speedSetting == 1)
                currentWaypoint.maximumAcceleration = 15;
            else if (speedSetting == 3)
                currentWaypoint.maximumAcceleration = 15;
            else
                currentWaypoint.maximumAcceleration = 20;

            double dist_left = MoveToWaypoint(currentWaypoint);
            //point_in_sequence = "Waypoint " + current_waypoint_number.ToString() + ".";
                            
            if (extra_info)
            {
                shipIOHandler.Echo("Status: " + status);
                shipIOHandler.Echo("Moving to waypoint: " + (current_waypoint_number+1).ToString() + ".");
            }
            else
            {
                shipIOHandler.Echo("Moving to waypoint: " + (current_waypoint_number + 1).ToString() + ".");
            }

            var elapsed = DateTime.Now - scriptStartTime;
            shipIOHandler.Echo("\nTime elapsed: " + elapsed.Seconds + "." + 
                                elapsed.Milliseconds.ToString().Substring(0, 1));
            shipIOHandler.EchoFinish();

            return dist_left;
        }




        private double MoveToWaypoint(Waypoint waypoint)
        {
            //bool tempError = false;
            DeltaTime = Runtime.TimeSinceLastRun.TotalSeconds * 10;
            DeltaTimeReal = (DateTime.Now - previousTime).TotalSeconds;


            var CurrentVelocity = systemsAnalyzer.cockpit.GetShipVelocities().LinearVelocity;
            var VelocityChange = CurrentVelocity - previousVelocity;
            var ActualAcceleration = Vector3D.Zero;
            if (DeltaTimeReal > 0) ActualAcceleration = VelocityChange / DeltaTimeReal;

            systemsAnalyzer.UpdateThrusterGroupsWorldDirections();

            ThrusterGroup forceThrusterGroup = null;
            status = "ERROR";


            var UnknownAcceleration = -systemsAnalyzer.currentHomeLocation.stationAcceleration * safetyAcceleration;

            var Gravity_And_Unknown_Forces = (systemsAnalyzer.cockpit.GetNaturalGravity() + UnknownAcceleration) *
                                             systemsAnalyzer.shipMass;
            // + (systemsAnalyzer.currentHomeLocation.stationVelocity * DeltaTimeReal))

            Vector3D waypointPos = waypoint.position;

            if (waypoint.WaypointIsLocal)
            {
                waypointPos = HomeLocation.localPositionToWorldPosition(waypointPos, systemsAnalyzer.currentHomeLocation);
            }

            //var TargetRoute = waypointPos - systemsAnalyzer.currentHomeLocation.shipConnector.GetPosition();
            var TargetRoute = waypointPos - systemsAnalyzer.currentHomeLocation.shipConnector.GetPosition();

            if (waypoint.WaypointIsLocal)
            {
                TargetRoute = waypointPos - Me.CubeGrid.GetPosition();
            }

            var TargetDirection = Vector3D.Normalize(TargetRoute);
            var totalDistanceLeft = TargetRoute.Length();

            // Finding max forward thrust:
            var LeadVelocity = (CurrentVelocity - systemsAnalyzer.currentHomeLocation.stationVelocity).Length() +
                               DeltaTime * (waypoint.maximumAcceleration + issueDetection + add_acceleration);
            if (LeadVelocity > topSpeedUsed) LeadVelocity = topSpeedUsed;
            var TargetVelocity = TargetDirection * LeadVelocity + systemsAnalyzer.currentHomeLocation.stationVelocity;
            var velocityDifference = CurrentVelocity - TargetVelocity;
            double max_forward_acceleration;
            if (velocityDifference.Length() == 0)
            {
                max_forward_acceleration = 0;
            }
            else
            {
                var forward_thrust_direction = Vector3D.Normalize(TargetRoute);
                forward_thrust_direction = -Vector3D.Normalize(velocityDifference);
                forceThrusterGroup =
                    systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, forward_thrust_direction);
                if (forceThrusterGroup == null)
                {
                    //tempError = true;
                    shipIOHandler.Echo("Not enough thrust!");
                    previousVelocity = CurrentVelocity;
                    safetyAcceleration = 0;
                    previousTime = DateTime.Now;
                    return totalDistanceLeft;
                    //shipIOHandler.Error("Error! Unknown forces going on! Maybe you have artificial masses, or docked ships using their thrusters or such.\nError code 01");
                }

                max_forward_acceleration = forceThrusterGroup.lambdaResult / systemsAnalyzer.shipMass;
            }

            // Finding reverse thrust:
            var reverse_target_velocity = systemsAnalyzer.currentHomeLocation.stationVelocity;
            var reverse_velocity_difference = CurrentVelocity - reverse_target_velocity;
            double max_reverse_acceleration;
            if (reverse_velocity_difference.Length() == 0)
            {
                max_reverse_acceleration = 0;
            }
            else
            {
                var reverse_thrust_direction = -Vector3D.Normalize(TargetRoute);
                forceThrusterGroup =
                    systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, reverse_thrust_direction);
                if (forceThrusterGroup == null)
                {
                    //tempError = true;
                    shipIOHandler.Echo("Not enough thrust!");
                    safetyAcceleration = 0;
                    previousVelocity = CurrentVelocity;
                    previousTime = DateTime.Now;
                    return totalDistanceLeft;
                    //shipIOHandler.Error("Error! Unknown forces going on! Maybe you have artificial masses, or docked ships using their thrusters or such.\nError code 02");
                }

                max_reverse_acceleration = forceThrusterGroup.lambdaResult / systemsAnalyzer.shipMass;
            }


            double distanceToGetToZero = 0;
            var Accelerating = false;


            if (max_reverse_acceleration != 0)
            {
                double timeToGetToZero = 0;
                timeToGetToZero = reverse_velocity_difference.Length() /
                                  (max_reverse_acceleration * (1 - caution) * waypoint.PercentageOfMaxAcceleration);
                timeToGetToZero += DeltaTime;
                distanceToGetToZero = reverse_velocity_difference.Length() * timeToGetToZero / 2;
            }

            if (distanceToGetToZero + waypoint.required_accuracy < totalDistanceLeft) Accelerating = true;

            if (Accelerating)
            {
                var target_acceleration = -velocityDifference / DeltaTime;
                var target_thrust = target_acceleration * systemsAnalyzer.shipMass;

                var target_acceleration_amount = target_acceleration.Length();

                if (target_acceleration_amount > max_forward_acceleration)
                {
                    forceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces,
                        Vector3.Normalize(target_acceleration));
                    // Cannot be done in 1 frame so we just do max thrust
                    status = "Speeding up";
                }
                else
                {
                    forceThrusterGroup = systemsAnalyzer.SolvePartialThrust(Gravity_And_Unknown_Forces, target_thrust);
                    // Can be done within 1 frame
                    status = "Drifting";
                }
            }
            else

            {
                var target_acceleration2 = -reverse_velocity_difference / DeltaTime;
                var target_thrust2 = target_acceleration2 * systemsAnalyzer.shipMass;
                var target_acceleration_amount = target_acceleration2.Length();
                if (target_acceleration_amount > max_reverse_acceleration)
                {
                    forceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces,
                        Vector3.Normalize(target_acceleration2));
                    // Cannot be done in 1 frame so we just do max thrust
                    status = "Slowing down";
                }
                else
                {
                    forceThrusterGroup = systemsAnalyzer.SolvePartialThrust(Gravity_And_Unknown_Forces, target_thrust2);
                    // Can be done within 1 frame
                    status = "Finished";
                }
            }

            SetResultantForces(forceThrusterGroup);

            previousVelocity = CurrentVelocity;
            previousTime = DateTime.Now;
            return totalDistanceLeft;
        }


        private void SetResultantAcceleration(Vector3D Gravity_And_Unknown_Forces, Vector3D TargetForceDirection,
            double proportionOfThrustToUse)
        {
            var maxForceThrusterGroup = systemsAnalyzer.SolveMaxThrust(-Gravity_And_Unknown_Forces,
                TargetForceDirection, proportionOfThrustToUse);

            // Set the unused thrusters to be off.
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[3], 0);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[4], 0);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[5], 0);

            // Set used thrusters to their values
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[0],
                maxForceThrusterGroup.finalThrustForces.X);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[1],
                maxForceThrusterGroup.finalThrustForces.Y);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[2],
                maxForceThrusterGroup.finalThrustForces.Z);
        }

        private void SetResultantForces(ThrusterGroup maxForceThrusterGroup)
        {
            // Set the unused thrusters to be off.
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[3], 0);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[4], 0);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[5], 0);

            // Set used thrusters to their values
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[0],
                maxForceThrusterGroup.finalThrustForces.X);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[1],
                maxForceThrusterGroup.finalThrustForces.Y);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[2],
                maxForceThrusterGroup.finalThrustForces.Z);
        }

        private void ConnectAndDock()
        {
            systemsAnalyzer.currentHomeLocation.shipConnector.Connect();
            shipIOHandler.Clear();
            shipIOHandler.Echo("DOCKED\nThe ship has docked " + your_title +
                               "!\nI will patiently await for more orders in the future.");
            shipIOHandler.EchoFinish();
            shipIOHandler.OutputTimer();
            SafelyExit();
        }

        private void SafelyExit()
        {
            Runtime.UpdateFrequency |= UpdateFrequency.Update1;
            scriptEnabled = false;
            runningIssues = "";
            if (systemsAnalyzer != null)
            {
                foreach (var thisGyro in systemsAnalyzer.gyros)
                    if (thisGyro != null)
                        if (thisGyro.IsWorking)
                            thisGyro.SetValue("Override", false);

                foreach (var thisThruster in systemsAnalyzer.thrusters)
                    if (thisThruster != null)
                        if (thisThruster.IsWorking)
                            thisThruster.SetValue("Override", 0f);
            }
        }


    }
}