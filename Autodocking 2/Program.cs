using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Begins the ship docking sequence. Requires (Will require) a HomeLocation and argument.
        /// </summary>
        /// <param name="beginConnector"></param>
        /// <param name="argument"></param>
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



        /// <summary>
        /// Equivalent to "Update()" from Unity but specifically for the docking sequence.
        /// </summary>
        void DockingSequenceFrameUpdate()
        {
                AlignWithGravity();
                var velocity = systemsAnalyzer.cockpit.GetShipVelocities().LinearVelocity;
                float m = 0;
                SetResultantAcceleration(-(float)velocity.X * m, -(float)velocity.Y * m, -(float)velocity.Z * m);
        }


        void SetResultantAcceleration(float xForce, float yForce, float zForce)
        {
            var CurrentFeltForce = systemsAnalyzer.cockpit.GetTotalGravity();
            var CurrentFeltForceAmount = CurrentFeltForce.Length();

            var ForceToApply = (new Vector3D(xForce, yForce, zForce) - CurrentFeltForce) * systemsAnalyzer.shipMass;

            //shipIOHandler.Echo ("Force to apply: " + ForceToApply.Length ().ToString ("0,0"));



            IMyShipConnector referenceBlock = systemsAnalyzer.myConnector;
            var referenceOrigin = referenceBlock.GetPosition();

            var block_WorldMatrix = VRageMath.Matrix.CreateWorld(referenceOrigin,
                referenceBlock.WorldMatrix.Up, //referenceBlock.WorldMatrix.Forward,
                -referenceBlock.WorldMatrix.Forward //referenceBlock.WorldMatrix.Up
            );

            var thrustForward = block_WorldMatrix.Forward;
            var thrustLeft = block_WorldMatrix.Left;
            var thrustUp = block_WorldMatrix.Up;

            var normalizedForcetoApply = Vector3D.Normalize(ForceToApply);
            if (thrustForward.Dot(normalizedForcetoApply) < 0)
            {
                thrustForward *= -1;
            }
            if (thrustLeft.Dot(normalizedForcetoApply) < 0)
            {
                thrustLeft *= -1;
            }
            if (thrustUp.Dot(normalizedForcetoApply) < 0)
            {
                thrustUp *= -1;
            }
            List<IMyThrust> ForwardThrusters = new List<IMyThrust>();
            List<IMyThrust> LeftThrusters = new List<IMyThrust>();
            List<IMyThrust> UpThrusters = new List<IMyThrust>();

            foreach (IMyThrust thisThruster in systemsAnalyzer.thrusters)
            {
                var thrusterDirection = -thisThruster.WorldMatrix.Forward;
                double forwardDot = Vector3D.Dot(thrusterDirection, Vector3D.Normalize(thrustForward));
                double leftDot = Vector3D.Dot(thrusterDirection, Vector3D.Normalize(thrustLeft));
                double upDot = Vector3D.Dot(thrusterDirection, Vector3D.Normalize(thrustUp));

                if (forwardDot >= 0.97)
                {
                    ForwardThrusters.Add(thisThruster);
                }
                else if (leftDot >= 0.97)
                {
                    LeftThrusters.Add(thisThruster);
                }
                else if (upDot >= 0.97)
                {
                    UpThrusters.Add(thisThruster);
                }
                else
                {
                    thisThruster.ThrustOverride = 0f;
                }
            }
            double ForwardMaxThrust = 0;
            double LeftMaxThrust = 0;
            double UpMaxThrust = 0;
            foreach (IMyThrust thisThruster in ForwardThrusters)
            {
                ForwardMaxThrust += thisThruster.MaxEffectiveThrust;
            }
            foreach (IMyThrust thisThruster in LeftThrusters)
            {
                LeftMaxThrust += thisThruster.MaxEffectiveThrust;
            }
            foreach (IMyThrust thisThruster in UpThrusters)
            {
                UpMaxThrust += thisThruster.MaxEffectiveThrust;
            }
            //shipIOHandler.Echo (ForwardMaxThrust.ToString ("0,0"));
            //shipIOHandler.Echo (LeftMaxThrust.ToString ("0,0"));
            //shipIOHandler.Echo (UpMaxThrust.ToString ("0,0") + "\n");

            double ForwardThrustToApply = 0;
            double LeftThrustToApply = 0;
            double UpThrustToApply = 0;

            var mat = new double[,] { { thrustForward.X, thrustLeft.X, thrustUp.X }, { thrustForward.Y, thrustLeft.Y, thrustUp.Y }, { thrustForward.Z, thrustLeft.Z, thrustUp.Z },
            };

            var ans = new double[] { ForceToApply.X, ForceToApply.Y, ForceToApply.Z };
            PID.ComputeCoefficients(mat, ans);

            // shipIOHandler.Echo (ans[0].ToString ());
            // shipIOHandler.Echo (ans[1].ToString ());
            // shipIOHandler.Echo (ans[2].ToString ());
            ForwardThrustToApply = ans[0];
            LeftThrustToApply = ans[1];
            UpThrustToApply = ans[2];

            double ForwardThrustProportion = ForwardThrustToApply / ForwardMaxThrust;
            double LeftThrustProportion = LeftThrustToApply / LeftMaxThrust;
            double UpThrustProportion = UpThrustToApply / UpMaxThrust;

            foreach (IMyThrust thisThruster in ForwardThrusters)
            {
                thisThruster.ThrustOverride = (float)(thisThruster.MaxThrust * ForwardThrustProportion);
                //thisThruster.ThrustOverride = thisThruster.MaxThrust;
            }
            foreach (IMyThrust thisThruster in LeftThrusters)
            {
                thisThruster.ThrustOverride = (float)(thisThruster.MaxThrust * (float)LeftThrustProportion);
                //thisThruster.ThrustOverride = thisThruster.MaxThrust;
            }
            foreach (IMyThrust thisThruster in UpThrusters)
            {
                thisThruster.ThrustOverride = (float)(thisThruster.MaxThrust * (float)UpThrustProportion);
                //thisThruster.ThrustOverride = thisThruster.MaxThrust;
            }

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
