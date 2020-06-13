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

        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts

        const double updatesPerSecond = 10;

        
        string current_argument;

        List<Vector3D> shipForwardLocal = new List<Vector3D>();
        List<Vector3D> shipUpwardLocal = new List<Vector3D>();

        ShipSystemsAnalyzer systemsAnalyzer;
        IOHandler shipIOHandler;



        bool errorState = false;
        static bool scriptEnabled = false;
        //string persistantText = "";

        double angleRoll = 0;
        double anglePitch = 0;

        const double proportionalConstant = 2;
        const double derivativeConstant = .5;
        const double timeLimit = 1 / updatesPerSecond;
        double timeElapsed = 0;

        PID pitchPID;
        PID rollPID;

        List<HomeLocation> homeLocations = new List<HomeLocation>();


        public Program()
        {
            errorState = false;
            Runtime.UpdateFrequency = UpdateFrequency.Once;

            systemsAnalyzer = new ShipSystemsAnalyzer(this);
            shipIOHandler = new IOHandler(this);

            pitchPID = new PID(proportionalConstant, 0, derivativeConstant, -10, 10, timeLimit);
            rollPID = new PID(proportionalConstant, 0, derivativeConstant, -10, 10, timeLimit);

            
            timeElapsed = 0;
            SafelyExit();
        }

        void OutputHomeLocations()
        {
            shipIOHandler.Echo("\n   Home location Data:");
            foreach (HomeLocation currentHomeLocation in homeLocations)
            {
                shipIOHandler.Echo("Station conn: " + currentHomeLocation.station_connector_name);
                IMyShipConnector my_connector = (IMyShipConnector)GridTerminalSystem.GetBlockWithId(currentHomeLocation.my_connector_ID);
                shipIOHandler.Echo("Ship conn: " + my_connector.CustomName);
                string argStr = "ARGS: ";
                foreach (string arg in currentHomeLocation.arguments)
                {
                    argStr += arg + ", ";
                }
                shipIOHandler.Echo(argStr + "\n");

            }
        }

        //Yeah thanks a lot, Whip.
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
            angleRoll = VectorAngleBetween(referenceLeft, planetRelativeLeftVec);
            angleRoll *= VectorCompareDirection(VectorProjection(referenceLeft, gravityVec), gravityVec); //ccw is positive 

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
                ApplyGyroOverride(pitchSpeed, 0, -rollSpeed, systemsAnalyzer.gyros, block_WorldMatrix);

            }
            else
            {
                return;
            }
        }

        




        void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, MatrixD b_WorldMatrix)
        {
            var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed); //because keen does some weird stuff with signs 
            var relativeRotationVec = Vector3D.TransformNormal(rotationVec, b_WorldMatrix);

            foreach (var thisGyro in gyro_list)
            {
                var gyroMatrix = thisGyro.WorldMatrix;
                var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(gyroMatrix));

                thisGyro.Pitch = (float)transformedRotationVec.X;
                thisGyro.Yaw = (float)transformedRotationVec.Y;
                thisGyro.Roll = (float)transformedRotationVec.Z;
                thisGyro.GyroOverride = true;
            }
        }

        public void Save()
        {

        }

        public void Begin(IMyShipConnector beginConnector, string argument)
        {
            //persistantText = "";
            current_argument = argument;
            scriptEnabled = true;
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            systemsAnalyzer.myConnector = beginConnector;
            OutputHomeLocations();
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
                        if (scriptEnabled)
                        {
                            if (argument == current_argument)
                            {
                                shipIOHandler.Echo("STOPPED\nAwaiting orders, Your Grace");
                                SafelyExit();
                            }
                            else
                            {
                                if (argument == "")
                                {
                                    shipIOHandler.Echo("RUNNING\nRe-starting docking sequence\nwith no argument.");
                                }
                                else
                                {
                                    shipIOHandler.Echo("RUNNING\nRe-starting docking sequence\nwith new argument: " + argument);
                                }
                                Begin(systemsAnalyzer.myConnector, argument);
                            }
                        }
                        else
                        {
                            if (argument == "")
                            {
                                shipIOHandler.Echo("RUNNING\nAttempting docking sequence\nwith no argument.");
                            }
                            else
                            {
                                shipIOHandler.Echo("RUNNING\nAttempting docking sequence\nwith argument: " + argument);
                            }

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

            if (scriptEnabled && !errorState)
            {
                timeElapsed += Runtime.TimeSinceLastRun.TotalSeconds;

                if (timeElapsed >= timeLimit)
                {

                    systemsAnalyzer.CheckForMassChange();

                    AlignWithGravity();
                    var velocity = systemsAnalyzer.cockpit.GetShipVelocities().LinearVelocity;
                    //shipIOHandler.Echo (velocity);
                    float m = 0;
                    SetResultantAcceleration(-(float)velocity.X * m, -(float)velocity.Y * m, -(float)velocity.Z * m);
                    timeElapsed = 0;
                }
            }

        }

        void MoveToPoint(float precision, Vector3D WorldPosition)
        {

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
            ComputeCoefficients(mat, ans);

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

        public void ComputeCoefficients(double[,] X, double[] Y)
        {
            int I, J, K, K1, N;
            N = Y.Length;
            for (K = 0; K < N; K++)
            {
                K1 = K + 1;
                for (I = K; I < N; I++)
                {
                    if (X[I, K] != 0)
                    {
                        for (J = K1; J < N; J++)
                        {
                            X[I, J] /= X[I, K];
                        }
                        Y[I] /= X[I, K];
                    }
                }
                for (I = K1; I < N; I++)
                {
                    if (X[I, K] != 0)
                    {
                        for (J = K1; J < N; J++)
                        {
                            X[I, J] -= X[K, J];
                        }
                        Y[I] -= Y[K];
                    }
                }
            }
            for (I = N - 2; I >= 0; I--)
            {
                for (J = N - 1; J >= I + 1; J--)
                {
                    Y[I] -= X[I, J] * Y[J];
                }
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

        #region PID

        int VectorCompareDirection(Vector3D a, Vector3D b) //returns -1 if vectors return negative dot product 
        {
            double check = a.Dot(b);
            if (check < 0)
                return -1;
            else
                return 1;
        }

        double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
        {
            if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
        }

        Vector3D VectorProjection(Vector3D a, Vector3D b) //proj a on b    
        {
            Vector3D projection = a.Dot(b) / b.LengthSquared() * b;
            return projection;
        }

        //Whip's PID controller class v6 - 11/22/17
        public class PID
        {
            double _kP = 0;
            double _kI = 0;
            double _kD = 0;
            double _integralDecayRatio = 0;
            double _lowerBound = 0;
            double _upperBound = 0;
            double _timeStep = 0;
            double _inverseTimeStep = 0;
            double _errorSum = 0;
            double _lastError = 0;
            bool _firstRun = true;
            bool _integralDecay = false;
            public double Value { get; private set; }

            public PID(double kP, double kI, double kD, double lowerBound, double upperBound, double timeStep)
            {
                _kP = kP;
                _kI = kI;
                _kD = kD;
                _lowerBound = lowerBound;
                _upperBound = upperBound;
                _timeStep = timeStep;
                _inverseTimeStep = 1 / _timeStep;
                _integralDecay = false;
            }

            public PID(double kP, double kI, double kD, double integralDecayRatio, double timeStep)
            {
                _kP = kP;
                _kI = kI;
                _kD = kD;
                _timeStep = timeStep;
                _inverseTimeStep = 1 / _timeStep;
                _integralDecayRatio = integralDecayRatio;
                _integralDecay = true;
            }

            public double Control(double error)
            {
                //Compute derivative term
                var errorDerivative = (error - _lastError) * _inverseTimeStep;

                if (_firstRun)
                {
                    errorDerivative = 0;
                    _firstRun = false;
                }

                //Compute integral term
                if (!_integralDecay)
                {
                    _errorSum += error * _timeStep;

                    //Clamp integral term
                    if (_errorSum > _upperBound)
                        _errorSum = _upperBound;
                    else if (_errorSum < _lowerBound)
                        _errorSum = _lowerBound;
                }
                else
                {
                    _errorSum = _errorSum * (1.0 - _integralDecayRatio) + error * _timeStep;
                }

                //Store this error as last error
                _lastError = error;

                //Construct output
                this.Value = _kP * error + _kI * _errorSum + _kD * errorDerivative;
                return this.Value;
            }

            public double Control(double error, double timeStep)
            {
                _timeStep = timeStep;
                _inverseTimeStep = 1 / _timeStep;
                return Control(error);
            }

            public void Reset()
            {
                _errorSum = 0;
                _lastError = 0;
                _firstRun = true;
            }
        }

        #endregion
    }
}
