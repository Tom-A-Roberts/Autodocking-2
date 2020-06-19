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
                //AlignWithGravity();
                var velocity = systemsAnalyzer.cockpit.GetShipVelocities().LinearVelocity;

            //SetResultantAcceleration(Vector3D.Normalize(-systemsAnalyzer.cockpit.WorldMatrix.Forward + systemsAnalyzer.cockpit.WorldMatrix.Left));
            //systemsAnalyzer.cockpit.WorldMatrix.Forward
            SetResultantAcceleration(new Vector3D(-0.8776,0.436,-0.1988));
        }


        void SetResultantAcceleration(Vector3D TargetForceDirection)
        {
            systemsAnalyzer.UpdateThrusterGroupsWorldDirections();

            var UnknownAcceleration = Vector3.Zero;
            var Gravity_And_Unknown_Forces = (systemsAnalyzer.cockpit.GetTotalGravity() + UnknownAcceleration) * systemsAnalyzer.shipMass;



            bool use_new_method = false;



            ThrusterGroup maxForceThrusterGroup = systemsAnalyzer.SolveMaxThrust(Gravity_And_Unknown_Forces, -TargetForceDirection);

            // Output rating system:
            if (maxForceThrusterGroup != null)
            {
                Vector3D resultantDirection = systemsAnalyzer.FindActualForceFromThrusters(maxForceThrusterGroup);
                Vector3D resultantNoGrav = resultantDirection - Gravity_And_Unknown_Forces;
                double dot_rating = Vector3D.Dot(Vector3D.Normalize(TargetForceDirection), Vector3D.Normalize(resultantNoGrav));
                shipIOHandler.Echo("Dot Rating: " + IOHandler.RoundToSignificantDigits(dot_rating, 3).ToString());
            }
            else
            {
                shipIOHandler.Echo("Dot Rating: ERROR");
                use_new_method = false;
            }

            //shipIOHandler.Echo(Vector3D.Normalize(Gravity_And_Unknown_Forces));








            if (use_new_method)
            {
                ThrusterGroup[] thrusterGroupsToUse = maxForceThrusterGroup.finalThrusterGroups;
                systemsController.SetThrusterForces(thrusterGroupsToUse[3], 0);
                systemsController.SetThrusterForces(thrusterGroupsToUse[4], 0);
                systemsController.SetThrusterForces(thrusterGroupsToUse[5], 0);

                // Set used thrusters to their values
                systemsController.SetThrusterForces(thrusterGroupsToUse[0], maxForceThrusterGroup.finalThrustForces.X);
                systemsController.SetThrusterForces(thrusterGroupsToUse[1], maxForceThrusterGroup.finalThrustForces.Y);
                systemsController.SetThrusterForces(thrusterGroupsToUse[2], maxForceThrusterGroup.finalThrustForces.Z);
            }
            else
            {
                // Using old deprecated method just so that the thrusters can stay afloat:
                ThrusterGroup[] thrusterGroupsToUse = systemsAnalyzer.FindThrusterGroupsInDirection(Gravity_And_Unknown_Forces);
                double[] thrustsNeededOverall = systemsAnalyzer.CalculateThrusterGroupsPower(Gravity_And_Unknown_Forces, thrusterGroupsToUse);

                //shipIOHandler.Echo("1: " + (thrustsNeededOverall.X / thrusterGroupsToUse[0].MaxThrust).ToString());
                //shipIOHandler.Echo("2: " + (thrustsNeededOverall.Y / thrusterGroupsToUse[1].MaxThrust).ToString());
                //shipIOHandler.Echo("3: " + (thrustsNeededOverall.Z / thrusterGroupsToUse[2].MaxThrust).ToString());

                // Set unused thrusters to 0
                systemsController.SetThrusterForces(thrusterGroupsToUse[3], 0);
                systemsController.SetThrusterForces(thrusterGroupsToUse[4], 0);
                systemsController.SetThrusterForces(thrusterGroupsToUse[5], 0);

                // Set used thrusters to their values
                systemsController.SetThrusterForces(thrusterGroupsToUse[0], thrustsNeededOverall[0]);
                systemsController.SetThrusterForces(thrusterGroupsToUse[1], thrustsNeededOverall[1]);
                systemsController.SetThrusterForces(thrusterGroupsToUse[2], thrustsNeededOverall[2]);
                //systemsController.SetThrusterForces(thrusterGroupsToUse[0], maxForceThrusterGroup.finalThrustForces.X);
                //systemsController.SetThrusterForces(thrusterGroupsToUse[1], maxForceThrusterGroup.finalThrustForces.Y);
                //systemsController.SetThrusterForces(thrusterGroupsToUse[2], maxForceThrusterGroup.finalThrustForces.Z);
                //systemsController.SetThrusterForces(thrusterGroupsToUse[0], thrustsNeededOverall.X);
                //systemsController.SetThrusterForces(thrusterGroupsToUse[1], thrustsNeededOverall.Y);
                //systemsController.SetThrusterForces(thrusterGroupsToUse[2], thrustsNeededOverall.Z);

            }
            


            shipIOHandler.EchoFinish(false, 1.6f);


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
