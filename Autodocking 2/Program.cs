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
            if (Storage.Length > 0)
                RetrieveStorage();

            shipIOHandler = new IOHandler(this);
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
                    homeLocations.Add(new HomeLocation(dataItem, this));
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
        void AlignWithGravity()
        {
            IMyShipConnector referenceBlock = systemsAnalyzer.currentHomeLocation.shipConnector;

            var referenceOrigin = referenceBlock.GetPosition();
            var gravityVec = systemsAnalyzer.cockpit.GetNaturalGravity();
            var gravityVecLength = gravityVec.Length();
            if (gravityVec.LengthSquared() == 0)
            {
                //Echo("No gravity");
                foreach (IMyGyro thisGyro in systemsAnalyzer.gyros)
                {
                    thisGyro.SetValue("Override", false);
                }
                return;
            }
            //var block_WorldMatrix = referenceBlock.WorldMatrix;
            var block_WorldMatrix = VRageMath.Matrix.CreateWorld(referenceOrigin,
                referenceBlock.WorldMatrix.Up, //referenceBlock.WorldMatrix.Forward,
                -referenceBlock.WorldMatrix.Forward //referenceBlock.WorldMatrix.Up
            );

            // var referenceForward = block_WorldMatrix.Forward;
            // var referenceLeft = block_WorldMatrix.Left;
            // var referenceUp = block_WorldMatrix.Up;

            var referenceForward = block_WorldMatrix.Forward;
            var referenceLeft = block_WorldMatrix.Left;
            var referenceUp = block_WorldMatrix.Up;

            anglePitch = Math.Acos(MathHelper.Clamp(gravityVec.Dot(referenceForward) / gravityVecLength, -1, 1)) - Math.PI / 2;
            Vector3D planetRelativeLeftVec = referenceForward.Cross(gravityVec);
            angleRoll = PID.VectorAngleBetween(referenceLeft, planetRelativeLeftVec);
            angleRoll *= PID.VectorCompareDirection(PID.VectorProjection(referenceLeft, gravityVec), gravityVec); //ccw is positive 

            anglePitch *= -1;
            angleRoll *= -1;

            // Echo ("pitch angle:" + Math.Round ((anglePitch / Math.PI * 180), 2).ToString () + " deg");
            // Echo ("roll angle:" + Math.Round ((angleRoll / Math.PI * 180), 2).ToString () + " deg");

            double rawDevAngle = Math.Acos(MathHelper.Clamp(gravityVec.Dot(referenceForward) / gravityVec.Length() * 180 / Math.PI, -1, 1));

            double rollSpeed = rollPID.Control(angleRoll);
            double pitchSpeed = pitchPID.Control(anglePitch);

            //---Set appropriate gyro override  
            if (!errorState)
            {
                //do gyros
                systemsController.ApplyGyroOverride(pitchSpeed, 0, -rollSpeed, systemsAnalyzer.gyros, block_WorldMatrix);

            }
            else
            {
                return;
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
            //AlignWithGravity();
            //Vector3D waypoint1 = new Vector3D(13310.29, 143907.58, -108825.71);
            //double accuracy = MoveToWaypoint(waypoint1);




            shipIOHandler.DockingSequenceStartMessage(current_argument);
            if (extra_info)
            {
                shipIOHandler.Echo("Status: " + status);
            }
            TimeSpan elapsed = System.DateTime.Now - scriptStartTime;
            shipIOHandler.Echo("\nTime elapsed: " + elapsed.Seconds.ToString() + "." + elapsed.Milliseconds.ToString().Substring(0,1));
            shipIOHandler.EchoFinish(false);
        }

        /// <summary>Equivalent to "Update()" from Unity but specifically for the docking sequence.</summary>
        double MoveToWaypoint(Vector3D WaypointPosition)
        {

            const double accuracy_allowance = 0.1;

            systemsAnalyzer.UpdateThrusterGroupsWorldDirections();
            
            var CurrentVelocity = systemsAnalyzer.cockpit.GetShipVelocities().LinearVelocity;

            ThrusterGroup forceThrusterGroup;
            status = "ERROR";

            var UnknownAcceleration = Vector3.Zero;
            var Gravity_And_Unknown_Forces = (systemsAnalyzer.cockpit.GetTotalGravity() + UnknownAcceleration) * systemsAnalyzer.shipMass;

            Vector3D TargetRoute = WaypointPosition - systemsAnalyzer.cockpit.GetPosition();
            Vector3D TargetDirection = Vector3D.Normalize(TargetRoute);
            double totalDistanceLeft = TargetRoute.Length();


            // Finding max forward thrust:
            double LeadVelocity = (CurrentVelocity - platformVelocity).Length() + 1;
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
                max_reverse_acceleration = (forceThrusterGroup.lambdaResult) / systemsAnalyzer.shipMass;
            }

            // Finding predictive point:
            #region attempt 1


            //topSpeedAchievable = Math.Sqrt((2 * distanceToTarget) / ((1 / max_forward_acceleration) + (1 / max_reverse_acceleration)));
            //shipIOHandler.Echo("Top achievable: " + IOHandler.RoundToSignificantDigits(topSpeedAchievable,2) + " m/s");

            //if (topSpeedAchievable > topSpeed)
            //{
            //    // In the case of a trapezium graph:
            //    if (velocityDifference.Length() <= ERROR_ALLOWANCE)
            //        timeToGetToTopSpeed = 0;
            //    else
            //        timeToGetToTopSpeed = topSpeed / max_forward_acceleration;

            //    distanceToGetToTopSpeed = timeToGetToTopSpeed * topSpeed / 2;

            //    if ((TargetVelocity - platformVelocity).Length() <= ERROR_ALLOWANCE)
            //        timeToGetToZero = 0;
            //    else
            //        timeToGetToZero = topSpeed / max_reverse_acceleration;

            //    distanceToGetToZero = timeToGetToZero * topSpeed / 2;

            //    double distanceDrifting = distanceToTarget - (distanceToGetToTopSpeed + distanceToGetToZero);
            //    timeDrifting = distanceDrifting / topSpeed;
            //}
            //else
            //{
            //    // In the case of a triangular graph:
            //    timeDrifting = 0;

            //    if (velocityDifference.Length() <= ERROR_ALLOWANCE)
            //        distanceToGetToTopSpeed = 0;
            //    else
            //        distanceToGetToTopSpeed = (topSpeedAchievable * topSpeedAchievable) / (2 * max_forward_acceleration);

            //    if ((TargetVelocity - platformVelocity).Length() <= ERROR_ALLOWANCE)
            //        distanceToGetToZero = 0;
            //    else
            //        distanceToGetToZero = (topSpeedAchievable * topSpeedAchievable) / (2 * max_reverse_acceleration);

            //    timeToGetToTopSpeed = (2 * distanceToGetToTopSpeed) / topSpeedAchievable;
            //    timeToGetToZero = (2 * distanceToGetToZero) / topSpeedAchievable;
            //}
            //shipIOHandler.Echo("Time to top: " + IOHandler.RoundToSignificantDigits(timeToGetToTopSpeed, 2) + " s");
            //shipIOHandler.Echo("Time to zero: " + IOHandler.RoundToSignificantDigits(timeToGetToZero, 2) + " s");

            //shipIOHandler.Echo("Dist to top: " + IOHandler.RoundToSignificantDigits(distanceToGetToTopSpeed, 2) + " m");
            //shipIOHandler.Echo("Dist to zero: " + IOHandler.RoundToSignificantDigits(distanceToGetToZero, 2) + " m");

            //Vector3D point_after_acceleration = systemsAnalyzer.cockpit.GetPosition() + (Vector3D.Normalize(forwardAcceleration) * distanceToGetToTopSpeed);
            //Vector3D final_point = point_after_acceleration + (TargetVelocity * timeDrifting) + (-Vector3D.Normalize(reverseAcceleration) * distanceToGetToZero) + (CurrentVelocity * (timeDrifting+timeToGetToTopSpeed+timeToGetToZero));
            //Vector3D error_amount = TargetPosition - final_point;
            //error_amount = Vector3D.Zero;
            #endregion

            double DeltaTime =  Runtime.TimeSinceLastRun.TotalSeconds * 10;

            double timeToGetToZero = 0;
            double distanceToGetToZero = 0;
            

            bool Accelerating = false;


            if (max_reverse_acceleration != 0)
            {
                timeToGetToZero = reverse_velocity_difference.Length() / (max_reverse_acceleration * (1 - caution));
                timeToGetToZero += DeltaTime;
                distanceToGetToZero = (reverse_velocity_difference.Length() * timeToGetToZero) / 2;
            }
            if (distanceToGetToZero + accuracy_allowance < totalDistanceLeft)
            {
                Accelerating = true;
            }


            if (Accelerating)
            {
                //// Finding the partial/max thrust to perform
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
                Vector3D target_acceleration = -reverse_velocity_difference / DeltaTime;
                Vector3D target_thrust = target_acceleration * systemsAnalyzer.shipMass;
                double target_acceleration_amount = target_acceleration.Length();
                if (target_acceleration_amount > max_reverse_acceleration)
                {
                    forceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, Vector3.Normalize(target_acceleration), 1);
                    // Cannot be done in 1 frame so we just do max thrust
                    status = "Slowing down";
                }
                else
                {
                    forceThrusterGroup = systemsAnalyzer.SolvePartialThrust(Gravity_And_Unknown_Forces, target_thrust);
                    // Can be done within 1 frame
                    status = "Finished";
                }
            }
            
            //shipIOHandler.Echo("Status: " + status);

            #region attempt 1


            //if (distanceToGetToZero == 0)
            //{
            //    // It has reached the finish
            //    thrustPower = 0;
            //    status = "Finished";
            //}
            //else if(distanceToGetToTopSpeed == 0 && distanceToTarget > distanceToGetToZero)
            //{
            //    // It has reached top speed and is drifting
            //    if (error_amount.Length() > ERROR_ALLOWANCE)
            //        target_thrust_direction = Vector3D.Normalize(error_amount);
            //    else
            //        thrustPower = 0;
            //    status = "Drifting";
            //}
            //else if (distanceToGetToTopSpeed == 0 && distanceToTarget < distanceToGetToZero)
            //{
            //    // It is reducing in speed
            //    target_thrust_direction = Vector3D.Normalize(-CurrentVelocity + platformVelocity + error_amount);
            //    status = "Slowing down";
            //}
            //else
            //{
            //    // It is increasing in speed
            //    target_thrust_direction = Vector3D.Normalize(-velocityDifference + error_amount);
            //    status = "Speeding up";
            //}

            //forceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, target_thrust_direction, thrustPower);



            //if (topSpeed < topSpeedAchievable)
            //{
            //    // The ship will be reaching the top speed, aka trapezium graph.
            //    double distanceToGetToFullSpeed = (targetSpeed * targetSpeed) / (2 * (max_forward_acceleration / systemsAnalyzer.shipMass));
            //    distanceRequiredToStop = 
            //}

            //Vector3D reverse_target_direction = -Vector3D.Normalize(CurrentVelocity - platformVelocity);
            //Vector3D predictive_point = 



            //double reverse_lambda = forceThrusterGroup.lambdaResult * 1; // Leeway variable
            //double v = CurrentVelocity.Length();
            //double u = platformVelocity.Length();
            //double stopping_distance = (v * v - u * u) / (2 * (reverse_lambda / systemsAnalyzer.shipMass)); // Should likely be done per-component.
            //double distance_left = TargetRoute.Length();

            //if (distance_left <= stopping_distance){}
            //else
            //{
            //    Vector3D TargetVelocity = (TargetDirection * topSpeed) + platformVelocity;
            //    Vector3D velocityDifference = CurrentVelocity - TargetVelocity;
            //    forceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, -Vector3D.Normalize(velocityDifference), 1);
            //}

            #endregion

            SetResultantForces(forceThrusterGroup);
            

            return totalDistanceLeft;
        }


        void SetResultantAcceleration(Vector3D Gravity_And_Unknown_Forces, Vector3D TargetForceDirection, double proportionOfThrustToUse)
        {

            ThrusterGroup maxForceThrusterGroup = systemsAnalyzer.SolveMaxThrust(-Gravity_And_Unknown_Forces, TargetForceDirection, proportionOfThrustToUse);

            // Here for debug purposes:
            systemsAnalyzer.CheckForceFromThrusters(maxForceThrusterGroup, TargetForceDirection, Gravity_And_Unknown_Forces);



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
