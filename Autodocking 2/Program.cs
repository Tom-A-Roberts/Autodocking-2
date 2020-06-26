using EmptyKeys.UserInterface.Generated.DataTemplatesContracts_Bindings;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {

        // CHANGEABLE VARIABLES:

        const double updatesPerSecond = 10;             // Defines how many times the script performes it's calculations per second.
        const double topSpeed = 100;                    // The top speed the ship will go, m/s.
        const double caution = 0.4;                     // Between 0 - 0.9. Defines how close to max deceleration the ship will ride.
        bool extra_info = true;                  // If true, this script will give you more information about what's happening than usual.

        // DO NOT CHANGE BELOW THIS LINE \/ \/ \/

        // Script systems:
        ShipSystemsAnalyzer systemsAnalyzer;
        ShipSystemsController systemsController;
        IOHandler shipIOHandler;


        // Script States:
        string current_argument;
        bool errorState = false;
        bool scriptEnabled = false;
        string status = "";
        //string persistantText = "";
        double timeElapsed = 0;
        DateTime scriptStartTime;
        List<HomeLocation> homeLocations;// = new List<HomeLocation>();



        // Ship vector math variables:
        //List<Vector3D> shipForwardLocal = new List<Vector3D>();
        //List<Vector3D> shipUpwardLocal = new List<Vector3D>();
        double angleRoll = 0;
        double anglePitch = 0;
        PID pitchPID;
        PID rollPID;
        Vector3D platformVelocity;

        // Script constants:
        const double proportionalConstant = 2;
        const double derivativeConstant = .5;
        const double timeLimit = 1 / updatesPerSecond;
        

        // Thanks to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts

        public Program()
        {
            errorState = false;
            Runtime.UpdateFrequency = UpdateFrequency.Once;
            platformVelocity = Vector3D.Zero;
            homeLocations = new List<HomeLocation>();

            shipIOHandler = new IOHandler(this);

            if (Storage.Length > 0)
                RetrieveStorage();
            
            systemsAnalyzer = new ShipSystemsAnalyzer(this);
            systemsController = new ShipSystemsController();

            

            pitchPID = new PID(proportionalConstant, 0, derivativeConstant, -10, 10, timeLimit);
            rollPID = new PID(proportionalConstant, 0, derivativeConstant, -10, 10, timeLimit);
            
            timeElapsed = 0;
            SafelyExit();


            //if (extra_info)
            //{
                
            //}
            //shipIOHandler.EchoFinish(false);
        }

        void RetrieveStorage()
        {
            //Data 
            string[] two_halves = Storage.Split('#');
            string[] home_location_data = two_halves[0].Split(';');

            foreach (string dataItem in home_location_data)
            {
                if(dataItem.Length > 0)
                {
                    HomeLocation newLoc = new HomeLocation(dataItem, this);
                    if (newLoc.shipConnector != null)
                    {
                        homeLocations.Add(newLoc);
                    }
                }
            }
        }
        void SaveStorage()
        {
            Storage = "";
            //Data 
            foreach (HomeLocation homeLocation in homeLocations)
            {
                Storage += homeLocation.ProduceSaveData() + ";";
            }
            Storage += "#";
        }

        //Help from Whip.
        double AlignWithGravity(Waypoint waypoint)
        {
            if (waypoint.RequireRotation)
            {
            IMyShipConnector referenceBlock = systemsAnalyzer.currentHomeLocation.shipConnector;

            var referenceOrigin = referenceBlock.GetPosition();
            var targetDirection = -waypoint.forward;
            var gravityVecLength = targetDirection.Length();
            if (targetDirection.LengthSquared() == 0)
            {
                foreach (IMyGyro thisGyro in systemsAnalyzer.gyros)
                {
                    thisGyro.SetValue("Override", false);
                }
                return -1;
            }
            //var block_WorldMatrix = referenceBlock.WorldMatrix;
            var block_WorldMatrix = VRageMath.Matrix.CreateWorld(referenceOrigin,
                referenceBlock.WorldMatrix.Up, //referenceBlock.WorldMatrix.Forward,
                -referenceBlock.WorldMatrix.Forward //referenceBlock.WorldMatrix.Up
            );

            var referenceForward = block_WorldMatrix.Forward;
            var referenceLeft = block_WorldMatrix.Left;
            var referenceUp = block_WorldMatrix.Up;

            //var referenceForward = -referenceBlock.WorldMatrix.Up;
            //var referenceLeft = referenceBlock.WorldMatrix.Left;
            //var referenceUp = referenceBlock.WorldMatrix.Forward;

             anglePitch = Math.Acos(MathHelper.Clamp(targetDirection.Dot(referenceForward) / gravityVecLength, -1, 1)) - Math.PI / 2;
            Vector3D planetRelativeLeftVec = referenceForward.Cross(targetDirection);
            angleRoll = PID.VectorAngleBetween(referenceLeft, planetRelativeLeftVec);
            angleRoll *= PID.VectorCompareDirection(PID.VectorProjection(referenceLeft, targetDirection), targetDirection); //ccw is positive 

            anglePitch *= -1;
            angleRoll *= -1;

            //shipIOHandler.Echo("Pitch angle: " + Math.Round((anglePitch / Math.PI * 180), 2).ToString() + " deg");
            //shipIOHandler.Echo("Roll angle: " + Math.Round((angleRoll / Math.PI * 180), 2).ToString() + " deg");

            //double rawDevAngle = Math.Acos(MathHelper.Clamp(targetDirection.Dot(referenceForward) / targetDirection.Length() * 180 / Math.PI, -1, 1));
            double rawDevAngle = Math.Acos(MathHelper.Clamp(targetDirection.Dot(referenceForward), -1, 1)) * 180 / Math.PI;
            rawDevAngle -= 90;

            //shipIOHandler.Echo("Angle: " + rawDevAngle.ToString());


            double rollSpeed = rollPID.Control(angleRoll);
            double pitchSpeed = pitchPID.Control(anglePitch);

            //---Set appropriate gyro override  
            if (!errorState)
            {
                //do gyros
                systemsController.ApplyGyroOverride(pitchSpeed, 0, -rollSpeed, systemsAnalyzer.gyros, block_WorldMatrix);
            }
            return rawDevAngle;
            }
            else
            {
                return -1;
            }
        }

        public void Save()
        {
            SaveStorage();
        }

        /// <summary>Begins the ship docking sequence. Requires (Will require) a HomeLocation and argument.</summary><param name="beginConnector"></param><param name="argument"></param>
        public void Begin(string argument) // WARNING, NEED TO ADD HOME LOCATION IN FUTURE INSTEAD
        {
            systemsAnalyzer.currentHomeLocation = FindHomeLocation(argument);
            if (systemsAnalyzer.currentHomeLocation != null)
            {
                current_argument = argument;
                scriptEnabled = true;
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
                scriptStartTime = System.DateTime.Now;
                previousTime = System.DateTime.Now;
                previousVelocity = systemsAnalyzer.cockpit.GetShipVelocities().LinearVelocity;
            }
            else
            {
                SafelyExit();
            }

        }

        public string updateHomeLocation(string argument, IMyShipConnector my_connected_connector)
        {
            IMyShipConnector station_connector = my_connected_connector.OtherConnector;
            if (station_connector == null)
            {
                shipIOHandler.Error("\nSomething went wrong when finding the connector.\nMaybe you have multiple connectors on the go, captain?");
                return "";
            }
            // We create a new home location, so that it can be compared with all the others.
            HomeLocation newHomeLocation = new HomeLocation(argument, my_connected_connector, station_connector);
            int HomeLocationIndex = homeLocations.LastIndexOf(newHomeLocation);
            if (HomeLocationIndex != -1)
            {
                // Docking location that was just created, already exists.
                if (extra_info)
                    shipIOHandler.Echo("- Docking location already Exists!\n- Adding argument.");
                if (!homeLocations[HomeLocationIndex].arguments.Contains(argument))
                {
                    if (extra_info)
                    {
                        shipIOHandler.Echo("Other arguments associated: " + shipIOHandler.GetHomeLocationArguments(homeLocations[HomeLocationIndex]));
                    }
                    homeLocations[HomeLocationIndex].arguments.Add(argument);
                    if (extra_info)
                        shipIOHandler.Echo("- New argument added.");
                }
                else if (extra_info)
                {
                    shipIOHandler.Echo("- Argument already in!");
                    if (extra_info)
                    {
                        shipIOHandler.Echo("All arguments associated: " + shipIOHandler.GetHomeLocationArguments(homeLocations[HomeLocationIndex]));
                    }
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
            int amountFound = 0;
            List<HomeLocation> toDelete = new List<HomeLocation>();
            foreach (HomeLocation currentHomeLocation in homeLocations)
            {
                if (!currentHomeLocation.Equals(newHomeLocation)) {
                    if (currentHomeLocation.arguments.Contains(argument))
                    {
                        amountFound += 1;

                        currentHomeLocation.arguments.Remove(argument);
                        if(currentHomeLocation.arguments.Count == 0)
                        {
                            toDelete.Add(currentHomeLocation);
                        }
                    }
                }
            }
            //Predicate<Point> predicate1 = HasNoArguments;
            while (toDelete.Count > 0)
            {
                homeLocations.Remove(toDelete[0]);
                toDelete.RemoveAt(0);
            }
            

            if (extra_info)
            {
                if(amountFound == 1)
                    shipIOHandler.Echo("- Found 1 other association with that argument. Removed this other.");
                else if(amountFound > 1)
                    shipIOHandler.Echo("- Found " + amountFound.ToString() + " other associations with that argument. Removed these others.");
            }
                

            if (argument == "")
            {
                if (!extra_info)
                    return "SAVED\nSaved docking location as no argument";
                else
                    return "Saved docking location as no argument";
            }
            else
            {
                if (!extra_info)
                    return "SAVED\nSaved docking location as: " + argument;
                else
                    return "Saved docking location as: " + argument;
            }

        }

        public void Main(string argument, UpdateType updateSource)
        {

            if ((updateSource & (UpdateType.Update1 | UpdateType.Once)) == 0)
            {
                // Script is activated by pressing "Run"
                if (errorState)
                {
                    // If an error has happened
                    errorState = false;
                    systemsAnalyzer.GatherBasicData();
                }
                if (!errorState)
                {
                    // script was activated and there was no error so far.
                    var my_connected_connector = systemsAnalyzer.FindMyConnectedConnector();
                    //findConnectorCount += 1;
                    //shipIOHandler.Echo("Finding connector: " + findConnectorCount.ToString());

                    if (my_connected_connector == null)
                    {
                        if (scriptEnabled && argument == current_argument)
                        {
                            // Script was already running, and using current argument, therefore this is a stopping order.
                            shipIOHandler.Echo("STOPPED\nAwaiting orders, Captain.");
                            SafelyExit();
                        }
                        else
                        {
                            // Request to dock initialized.
                            Begin(argument);
                        }
                    }
                    else
                    {
                        var result = updateHomeLocation(argument, my_connected_connector);
                        shipIOHandler.Echo(result);
                        //shipIOHandler.Echo("\nThis location also has\nother arguments associated:");
                        SafelyExit();
                    }
                }
                shipIOHandler.EchoFinish(false);
            }

            // Script docking is running:
            if (scriptEnabled && !errorState)
            {
                timeElapsed += Runtime.TimeSinceLastRun.TotalSeconds;
                if (timeElapsed >= timeLimit)
                {
                    systemsAnalyzer.CheckForMassChange();
                    // Do docking sequence:
                    DockingSequenceFrameUpdate();
                    timeElapsed = 0;
                }
            }
        }
        HomeLocation FindHomeLocation(string argument)
        {
            int amountFound = 0;
            HomeLocation resultantHomeLocation = null;
            foreach (HomeLocation currentHomeLocation in homeLocations)
            {
                if (currentHomeLocation.arguments.Contains(argument))
                {
                    amountFound += 1;
                    if (resultantHomeLocation == null)
                    {
                        resultantHomeLocation = currentHomeLocation;
                    }
                }
            }
            if (amountFound > 1)
            {
                shipIOHandler.Echo("Minor Warning:\nThere are " + amountFound.ToString() + " places\nthat argument is associated with!\nPicking first one found.");
            }
            else if (amountFound == 0)
            {
                shipIOHandler.Echo("WARNING:\nNo docking location found with that argument.\nPlease dock to a connector and press 'Run' with your argument\nto save it as a docking location.");
            }
            return resultantHomeLocation;
        }


        void DockingSequenceFrameUpdate()
        {
            if (systemsAnalyzer.currentHomeLocation.shipConnector.Status == MyShipConnectorStatus.Connectable)
            {
                systemsAnalyzer.currentHomeLocation.shipConnector.Connect();
                shipIOHandler.Echo("DOCKED\nThe ship has docked captain!\nI will patiently await for more orders in the future.");
                shipIOHandler.EchoFinish();
                SafelyExit();
            }
            else
            {
            shipIOHandler.DockingSequenceStartMessage(current_argument);

                if (systemsAnalyzer.basicDataGatherRequired)
            {
                systemsAnalyzer.GatherBasicData();
                systemsAnalyzer.basicDataGatherRequired = false;
            }

            const double sideways_dist_needed_to_land = 3;
            const double height_needed_for_connector = 5;
            
            Vector3D ConnectorLocation = systemsAnalyzer.currentHomeLocation.stationConnectorPosition;
            Vector3D ConnectorDirection = systemsAnalyzer.currentHomeLocation.stationConnectorForward;
            Vector3D target_position = ConnectorLocation + (ConnectorDirection * height_needed_for_connector);
            Vector3D current_position = systemsAnalyzer.currentHomeLocation.shipConnector.GetPosition();

            string point_in_sequence = "Starting...";


            Waypoint aboveConnectorWaypoint = new Waypoint(target_position, ConnectorDirection);

            // Constantly ensure alignment
            double direction_accuracy = AlignWithGravity(aboveConnectorWaypoint);


            if (Math.Abs(direction_accuracy) < 15)
            {
                // Test if ship is behind the station connector:
                Vector3D pointOnConnectorAxis = PID.NearestPointOnLine(ConnectorLocation, ConnectorDirection, current_position);
                Vector3D heightDifference = pointOnConnectorAxis - ConnectorLocation;
                double signedHeightDistanceToConnector = ConnectorDirection.Dot(Vector3D.Normalize(heightDifference)) * heightDifference.Length();
                double sidewaysDistance = (current_position - pointOnConnectorAxis).Length();


                if (sidewaysDistance > sideways_dist_needed_to_land && signedHeightDistanceToConnector < height_needed_for_connector * 0.9)
                {
                    // The ship is behind the connector, so it needs to fly up to it so that it is on the correct side at least.
                    // Only then can it attempt to land.
                    const double overshoot = 2;
                    Waypoint SomewhereOnCorrectSide = new Waypoint(current_position + (ConnectorDirection * (-signedHeightDistanceToConnector + overshoot + height_needed_for_connector)), ConnectorDirection);
                    SomewhereOnCorrectSide.maximumAcceleration = 20;
                    SomewhereOnCorrectSide.required_accuracy = 0.8;

                    MoveToWaypoint(SomewhereOnCorrectSide);
                    point_in_sequence = "Behind target, moving to be in front";
                }
                else if(sidewaysDistance > sideways_dist_needed_to_land)
                {
                    
                    aboveConnectorWaypoint.maximumAcceleration = 15;

                    MoveToWaypoint(aboveConnectorWaypoint);
                    point_in_sequence = "Moving toward connector";
                }
                else
                {
                    double connectorHeight = systemsAnalyzer.currentHomeLocation.stationConnectorSize + ShipSystemsAnalyzer.GetRadiusOfConnector(systemsAnalyzer.currentHomeLocation.shipConnector);
                    Waypoint DockedToConnector = new Waypoint(ConnectorLocation + (ConnectorDirection * connectorHeight), ConnectorDirection);
                    DockedToConnector.maximumAcceleration = 3;

                    double acc = MoveToWaypoint(DockedToConnector);
                    point_in_sequence = "landing on connector";
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
            TimeSpan elapsed = System.DateTime.Now - scriptStartTime;
            shipIOHandler.Echo("\nTime elapsed: " + elapsed.Seconds.ToString() + "." + elapsed.Milliseconds.ToString().Substring(0,1));
            shipIOHandler.EchoFinish(false);
            }
        }

        Vector3D previousVelocity = Vector3D.Zero;
        DateTime previousTime;
        double issueDetection = 0;

        /// <summary>Equivalent to "Update()" from Unity but specifically for the docking sequence.</summary>
        double MoveToWaypoint(Waypoint waypoint)
        {
            double DeltaTime = Runtime.TimeSinceLastRun.TotalSeconds * 10;
            double DeltaTimeReal = (DateTime.Now - previousTime).TotalSeconds;
            

            var CurrentVelocity = systemsAnalyzer.cockpit.GetShipVelocities().LinearVelocity;
            Vector3D VelocityChange = CurrentVelocity - previousVelocity;
            Vector3D ActualAcceleration = Vector3D.Zero;
            if(DeltaTimeReal > 0)
            {
                ActualAcceleration = VelocityChange / DeltaTimeReal;
            }

            systemsAnalyzer.UpdateThrusterGroupsWorldDirections();

            ThrusterGroup forceThrusterGroup = null;
            status = "ERROR";

            var UnknownAcceleration = Vector3.Zero;
            var Gravity_And_Unknown_Forces = (systemsAnalyzer.cockpit.GetNaturalGravity() + UnknownAcceleration) * systemsAnalyzer.shipMass;

            Vector3D TargetRoute = waypoint.position - systemsAnalyzer.currentHomeLocation.shipConnector.GetPosition();
            Vector3D TargetDirection = Vector3D.Normalize(TargetRoute);
            double totalDistanceLeft = TargetRoute.Length();

            // Finding max forward thrust:
            double LeadVelocity = (CurrentVelocity - platformVelocity).Length() + (DeltaTime * (waypoint.maximumAcceleration + issueDetection));
            if (LeadVelocity > topSpeed)
            {
                LeadVelocity = topSpeed;
            }
            Vector3D TargetVelocity = (TargetDirection * LeadVelocity) + platformVelocity;
            Vector3D velocityDifference = CurrentVelocity - TargetVelocity;
            double max_forward_acceleration;
            if (velocityDifference.Length() == 0)
            {
                max_forward_acceleration = 0;
            }
            else
            {
                Vector3D forward_thrust_direction = Vector3D.Normalize(TargetRoute);
                forward_thrust_direction = -Vector3D.Normalize(velocityDifference);
                forceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, forward_thrust_direction, 1);
                if(forceThrusterGroup == null)
                {
                    Echo("Target direction: " + forward_thrust_direction.ToString());
                    Echo("Gravity: " + (Gravity_And_Unknown_Forces / systemsAnalyzer.shipMass).ToString());
                    shipIOHandler.Error("Error when calculating required ship thrusts!\nError code 01");
                }
                if (forceThrusterGroup.lambdaResult == null)
                {
                    shipIOHandler.Error("Error when calculating required ship thrusts!\nError code 02");
                }
                max_forward_acceleration = forceThrusterGroup.lambdaResult / systemsAnalyzer.shipMass;

            }
            // Finding reverse thrust:
            Vector3D reverse_target_velocity = platformVelocity;
            Vector3D reverse_velocity_difference = CurrentVelocity - reverse_target_velocity;
            double max_reverse_acceleration;
            if (reverse_velocity_difference.Length() == 0)
            {
                max_reverse_acceleration = 0;
            }
            else
            {
                Vector3D reverse_thrust_direction = -Vector3D.Normalize(TargetRoute);
                //reverse_thrust_direction = -Vector3D.Normalize(reverse_velocity_difference);
                forceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, reverse_thrust_direction, 1);
                if (forceThrusterGroup == null)
                {
                    shipIOHandler.Error("Error when calculating required ship thrusts!\nError code 03");
                }
                if (forceThrusterGroup.lambdaResult == null)
                {
                    shipIOHandler.Error("Error when calculating required ship thrusts!\nError code 04");
                }
                max_reverse_acceleration = (forceThrusterGroup.lambdaResult) / systemsAnalyzer.shipMass;
            }

            double timeToGetToZero = 0;
            double distanceToGetToZero = 0;

            bool Accelerating = false;


            if (max_reverse_acceleration != 0)
            {
                timeToGetToZero = reverse_velocity_difference.Length() / (max_reverse_acceleration * (1 - caution) * waypoint.PercentageOfMaxAcceleration);
                timeToGetToZero += DeltaTime;
                distanceToGetToZero = (reverse_velocity_difference.Length() * timeToGetToZero) / 2;
            }
            if (distanceToGetToZero + waypoint.required_accuracy < totalDistanceLeft)
            {
                Accelerating = true;
            }

            if (Accelerating)
            {

                Vector3D target_acceleration = -velocityDifference / DeltaTime;
                Vector3D target_thrust = target_acceleration * systemsAnalyzer.shipMass;

                double target_acceleration_amount = target_acceleration.Length();

                if (target_acceleration_amount > max_forward_acceleration)
                {
                    forceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, Vector3.Normalize(target_acceleration), 1);
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
                Vector3D target_acceleration2 = -reverse_velocity_difference / DeltaTime;
                Vector3D target_thrust2 = target_acceleration2 * systemsAnalyzer.shipMass;
                double target_acceleration_amount = target_acceleration2.Length();
                if (target_acceleration_amount > max_reverse_acceleration)
                {
                    forceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, Vector3.Normalize(target_acceleration2), 1);
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

            //shipIOHandler.Echo("target acc: " + IOHandler.RoundToSignificantDigits(forceThrusterGroup.lambdaResult / systemsAnalyzer.shipMass, 2).ToString());
            //systemsAnalyzer.CheckForceFromThrusters(forceThrusterGroup, Gravity_And_Unknown_Forces);
            //shipIOHandler.Echo("actual acc: " + IOHandler.RoundToSignificantDigits(ActualAcceleration.Length(), 2).ToString());

            if (ActualAcceleration.Length() < 0.5 && Math.Abs(forceThrusterGroup.lambdaResult / systemsAnalyzer.shipMass) > 0.5)
            {
                issueDetection += DeltaTimeReal * 6;
                if (issueDetection > 1.5)
                {
                    shipIOHandler.Echo("Warning:\nIt seems something is going wrong with ship acceleration.\nI will attempt to fix this and continue docking.\nIf it isn't docking still, please report this to Spug in the comments section of the script.\n");
                }
                
            }
            else if (issueDetection > 0)
            {
                issueDetection -= DeltaTimeReal * 1;
            }
            else if(issueDetection < 0)
            {
                issueDetection = 0;
            }


            SetResultantForces(forceThrusterGroup);

            previousVelocity = CurrentVelocity;
            previousTime = DateTime.Now;
            return totalDistanceLeft;
        }


        void SetResultantAcceleration(Vector3D Gravity_And_Unknown_Forces, Vector3D TargetForceDirection, double proportionOfThrustToUse)
        {

            ThrusterGroup maxForceThrusterGroup = systemsAnalyzer.SolveMaxThrust(-Gravity_And_Unknown_Forces, TargetForceDirection, proportionOfThrustToUse);

            // Here for debug purposes:
            //systemsAnalyzer.CheckForceFromThrusters(maxForceThrusterGroup, TargetForceDirection, Gravity_And_Unknown_Forces);



            // Set the unused thrusters to be off.
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[3], 0);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[4], 0);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[5], 0);

            // Set used thrusters to their values
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[0], maxForceThrusterGroup.finalThrustForces.X);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[1], maxForceThrusterGroup.finalThrustForces.Y);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[2], maxForceThrusterGroup.finalThrustForces.Z);
            

            
            #region OldSystem

            //            double maxPossibleThrust =
            //                Math.Max(systemsAnalyzer.ForwardThrust.MaxThrust,
            //                Math.Max(systemsAnalyzer.UpThrust.MaxThrust,
            //                Math.Max(systemsAnalyzer.LeftThrust.MaxThrust,
            //                Math.Max(systemsAnalyzer.BackwardThrust.MaxThrust,
            //                Math.Max(systemsAnalyzer.RightThrust.MaxThrust,systemsAnalyzer.DownThrust.MaxThrust)))));
            //;

            //            //double t = systemsAnalyzer.cockpit.GetShipVelocities().LinearVelocity.Length() / (maxAvailableThrustInTargetDirection / 1000);
            //            //shipIOHandler.Echo(IOHandler.RoundToSignificantDigits(t * 1000, 3));



            //            systemsAnalyzer.PopulateThrusterGroupsLeftoverThrust(Gravity_And_Unknown_Forces);
            //            double maxAvailableThrustInTargetDirection = systemsAnalyzer.FindMaxAvailableThrustInDirection(Vector3D.Normalize(TargetForceDirection));


            //            Vector3D TargetForce = ((Vector3D.Normalize(TargetForceDirection) * maxAvailableThrustInTargetDirection * 0.1) + Gravity_And_Unknown_Forces);
            //            //Vector3D FinalForce = Gravity_And_Unknown_Forces;

            //            double ForceInDirection = systemsAnalyzer.FindAmountOfForceInDirection(TargetForce, TargetForceDirection);



            //            //shipIOHandler.Echo("Max available forward:");
            //            //shipIOHandler.Echo(IOHandler.RoundToSignificantDigits(maxPossibleThrust / 1000, 3));


            //            ThrusterGroup[] thrusterGroupsToUse = systemsAnalyzer.FindThrusterGroupsInDirection(TargetForce);
            //            double[] thrustsNeededOverall = systemsAnalyzer.CalculateThrusterGroupsPower(TargetForce, thrusterGroupsToUse);


            //            Vector3D ActualPossibleForce = systemsAnalyzer.FindActualForceFromThrusters(thrusterGroupsToUse, thrustsNeededOverall);
            //            Vector3D ForceCorrected = ActualPossibleForce - Gravity_And_Unknown_Forces;
            //            double dot = Vector3D.Normalize(ForceCorrected).Dot(Vector3D.Normalize(TargetForceDirection));
            //            shipIOHandler.Echo("Dot: " + dot.ToString());
            #endregion
        }

        void SetResultantForces(ThrusterGroup maxForceThrusterGroup)
        {
            // Set the unused thrusters to be off.
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[3], 0);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[4], 0);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[5], 0);

            // Set used thrusters to their values
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[0], maxForceThrusterGroup.finalThrustForces.X);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[1], maxForceThrusterGroup.finalThrustForces.Y);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[2], maxForceThrusterGroup.finalThrustForces.Z);
        }

        void SetResultantForces(ThrusterGroupResult maxForceThrusterGroup)
        {
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[3], 0);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[4], 0);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[5], 0);

            // Set used thrusters to their values
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[0], maxForceThrusterGroup.finalThrustForces.X);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[1], maxForceThrusterGroup.finalThrustForces.Y);
            systemsController.SetThrusterForces(maxForceThrusterGroup.finalThrusterGroups[2], maxForceThrusterGroup.finalThrustForces.Z);
        }


        void SafelyExit()
        {
            //Runtime.UpdateFrequency = UpdateFrequency.Once;
            Runtime.UpdateFrequency |= UpdateFrequency.Update1;
            scriptEnabled = false;
            foreach (IMyGyro thisGyro in systemsAnalyzer.gyros)
            {
                thisGyro.SetValue("Override", false);
            }
            foreach (IMyThrust thisThruster in systemsAnalyzer.thrusters)
            {
                thisThruster.SetValue("Override", 0f);
            }
        }
    }
}
