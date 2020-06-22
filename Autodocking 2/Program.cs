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
        const double topSpeed = 15;                     // The top speed the ship will go, m/s.


        // DO NOT CHANGE BELOW THIS LINE \/ \/ \/

        // Script systems:
        ShipSystemsAnalyzer systemsAnalyzer;
        ShipSystemsController systemsController;
        IOHandler shipIOHandler;


        // Script States:
        string current_argument;
        bool errorState = false;
        bool scriptEnabled = false;
        //string persistantText = "";
        double timeElapsed = 0;
        List<HomeLocation> homeLocations = new List<HomeLocation>();


        // Ship vector math variables:
        List<Vector3D> shipForwardLocal = new List<Vector3D>();
        List<Vector3D> shipUpwardLocal = new List<Vector3D>();
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

            shipIOHandler = new IOHandler(this);
            systemsAnalyzer = new ShipSystemsAnalyzer(this);
            systemsController = new ShipSystemsController();

            pitchPID = new PID(proportionalConstant, 0, derivativeConstant, -10, 10, timeLimit);
            rollPID = new PID(proportionalConstant, 0, derivativeConstant, -10, 10, timeLimit);
            
            timeElapsed = 0;
            SafelyExit();
        }

        //Help from Whip.
        void AlignWithGravity()
        {
            IMyShipConnector referenceBlock = systemsAnalyzer.myConnector;

            var referenceOrigin = referenceBlock.GetPosition();
            var gravityVec = systemsAnalyzer.cockpit.GetNaturalGravity();
            var gravityVecLength = gravityVec.Length();
            if (gravityVec.LengthSquared() == 0)
            {
                Echo("No gravity");
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

        }

        /// <summary>Begins the ship docking sequence. Requires (Will require) a HomeLocation and argument.</summary><param name="beginConnector"></param><param name="argument"></param>
        public void Begin(IMyShipConnector beginConnector, string argument) // WARNING, NEED TO ADD HOME LOCATION IN FUTURE INSTEAD
        {
            //persistantText = "";
            shipIOHandler.DockingSequenceStartMessage(argument);
            current_argument = argument;
            scriptEnabled = true;
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            systemsAnalyzer.myConnector = beginConnector;
            shipIOHandler.OutputHomeLocations();
        }

        public string updateHomeLocation(string argument, IMyShipConnector my_connected_connector)
        {
            //List<IMyShipConnector> Connectors = new List<IMyShipConnector> ();
            // if (my_connected_connector.Status == MyShipConnectorStatus.Connectable) {
            //     my_connected_connector.Connect ();
            // }
            IMyShipConnector ship_connector = my_connected_connector.OtherConnector;
            if (ship_connector == null)
            {
                shipIOHandler.Error("\nSomething went wrong when finding the connector.\nMaybe you have multiple connectors on the go, captain?");
                return "";
            }

            HomeLocation newHomeLocation = new HomeLocation(argument, my_connected_connector, ship_connector);

            int HomeLocationIndex = homeLocations.LastIndexOf(newHomeLocation);
            if (HomeLocationIndex != -1)
            {
                shipIOHandler.Echo("\nHome location already In! Adding arg.");
                if (!homeLocations[HomeLocationIndex].arguments.Contains(argument))
                {
                    homeLocations[HomeLocationIndex].arguments.Add(argument);
                    shipIOHandler.Echo("\nArg added.");
                }
                else
                {
                    shipIOHandler.Echo("\nArg already in!");
                }
            }
            else
            {
                homeLocations.Add(newHomeLocation);
                shipIOHandler.Echo("\nAdded new home location.");
            }

            // if (homeLocations.Contains (argument)) {
            //     if (homeLocations[argument].my_connector_ID != my_connected_connector.EntityId || homeLocations[argument].station_connector_ID != ship_connector.EntityId) {
            //         homeLocations[argument].arguments.Remove (argument);
            //         // if (homeLocations[argument].arguments.Count == 0) {
            //         // }
            //         homeLocations[argument] = new HomeLocation (argument, my_connected_connector, ship_connector);
            //         //AKA the user has docked with a known argument using a new connector or it's a different location.

            //         //Echo("");
            //     }
            // }

            if (argument == "")
            {
                return "Saved home location as no argument";
            }
            else
            {
                return "Saved home location as:\n" + argument;
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {

            if ((updateSource & (UpdateType.Update1 | UpdateType.Once)) == 0)
            {
                if (errorState)
                {
                    errorState = false;
                    systemsAnalyzer.GatherBasicData();
                }
                if (!errorState)
                {
                    var my_connected_connector = systemsAnalyzer.FindMyConnectedConnector();
                    if (my_connected_connector == null)
                    {
                        if (scriptEnabled && argument == current_argument)
                        {
                            // Script was already running, and using current argument, therefore this is a stopping order.
                            shipIOHandler.Echo("STOPPED\nAwaiting orders, Your Grace");
                            SafelyExit();
                        }
                        else
                        {
                            // Request to dock initialized.
                            Begin(systemsAnalyzer.myConnector, argument);
                        }
                    }
                    else
                    {
                        var result = updateHomeLocation(argument, my_connected_connector);
                        shipIOHandler.Echo(result);
                        shipIOHandler.Echo("\nThis location also has\nother arguments associated:");
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


        /// <summary>Equivalent to "Update()" from Unity but specifically for the docking sequence.</summary>
        void DockingSequenceFrameUpdate()
        {
            systemsAnalyzer.UpdateThrusterGroupsWorldDirections();
            //AlignWithGravity();
            var CurrentVelocity = systemsAnalyzer.cockpit.GetShipVelocities().LinearVelocity;


            //Direction is up and slightly backwards
            //SetResultantAcceleration(Vector3D.Normalize(new Vector3D(-1,1,-0.5)));

            var UnknownAcceleration = Vector3.Zero;
            var Gravity_And_Unknown_Forces = (systemsAnalyzer.cockpit.GetTotalGravity() + UnknownAcceleration) * systemsAnalyzer.shipMass;

            Vector3D TargetPosition = new Vector3D(53568.16, -26658.89, 12056.86);
            Vector3D TargetRoute = TargetPosition - systemsAnalyzer.cockpit.GetPosition();
            Vector3D TargetDirection = Vector3D.Normalize(TargetRoute);


            // Finding max forward thrust:
            //Vector3D TargetVelocity = (TargetDirection * topSpeed) + platformVelocity;
            //Vector3D velocityDifference = CurrentVelocity - TargetVelocity;
            Vector3D TargetVelocity = (TargetDirection * topSpeed) + platformVelocity;
            Vector3D velocityDifference = CurrentVelocity - TargetVelocity;
            Vector3D forward_thrust_direction = Vector3D.Normalize(TargetRoute);
            forward_thrust_direction = -Vector3D.Normalize(velocityDifference);
            ThrusterGroup forceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, forward_thrust_direction, 1);
            double max_forward_acceleration = forceThrusterGroup.lambdaResult / systemsAnalyzer.shipMass;
            Vector3D forwardAcceleration = max_forward_acceleration * forward_thrust_direction;

            // Finding reverse thrust:
            //Vector3D reverse_target_thrust_direction = -Vector3D.Normalize(CurrentVelocity - platformVelocity);
            Vector3D reverse_thrust_direction = -Vector3D.Normalize(TargetRoute);//-Vector3D.Normalize(TargetVelocity - platformVelocity);
            forceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, reverse_thrust_direction, 1);
            double max_reverse_acceleration = forceThrusterGroup.lambdaResult / systemsAnalyzer.shipMass;
            Vector3D reverseAcceleration = max_reverse_acceleration * reverse_thrust_direction;


            // Finding predictive point:
            //double targetSpeed = velocityDifference.Length();
            double distanceToTarget = TargetRoute.Length();

            // In the case of a trapezium graph:
            double timeToGetToTopSpeed;
            double distanceToGetToTopSpeed;

            double timeToGetToZero;
            double distanceToGetToZero;

            double timeDrifting;

            
            double topSpeedAchievable;

            const double ERROR_ALLOWANCE = 0.5;

            // WARNING! 0 error!
            if (max_forward_acceleration <= 0.001 && max_forward_acceleration >= 0.001)
            {
                max_forward_acceleration = 0.001;
            }
            if (max_reverse_acceleration <= 0.001 && max_reverse_acceleration >= 0.001)
            {
                max_reverse_acceleration = 0.001;
            }

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

            double DeltaTime =  Runtime.TimeSinceLastRun.TotalSeconds;
            //shipIOHandler.Echo("Delta time: " + DeltaTime.TotalSeconds.ToString());
            #region Status Calculations
            distanceToGetToTopSpeed = 0;
            string status = "ERROR";
            double thrustPower = 1;
            if (distanceToGetToTopSpeed == 0)
            {
                status = "Drifting";
                thrustPower = 0;
            }
            else
            {
                status = "Speeding up";
                thrustPower = 1;
            }
            shipIOHandler.Echo("Status: " + status);
            #endregion

            //// Finding the partial/max thrust to perform
            Vector3D target_acceleration = -velocityDifference / DeltaTime;
            Vector3D target_thrust = target_acceleration * systemsAnalyzer.shipMass;
            double target_acceleration_amount = target_acceleration.Length();
            if (target_acceleration_amount > max_forward_acceleration)
            {
                forceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, Vector3.Normalize(target_acceleration), 1);
                // Cannot be done in 1 frame so we just do max thrust
            }
            else
            {
                forceThrusterGroup = systemsAnalyzer.SolvePartialThrust(Gravity_And_Unknown_Forces, target_thrust);
                // Can be done within 1 frame
            }


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
            
            //SetResultantAcceleration(Gravity_And_Unknown_Forces, reverse_target_direction, 1);
            //SetResultantAcceleration(Gravity_And_Unknown_Forces, -velocityDifference, accuracyRating);
            shipIOHandler.EchoFinish(false, 1.6f);
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
