using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        /// <summary>
        /// Whip's PID controller class v6 - 11/22/17<br />
        /// Most of the code is thanks to Whip. It's used to do some essential maths when rotating the ship.
        /// </summary>
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


            public static Vector3D VectorProjection(Vector3D a, Vector3D b) //proj a on b    
            {
                Vector3D projection = a.Dot(b) / b.LengthSquared() * b;
                return projection;
            }
            public static int VectorCompareDirection(Vector3D a, Vector3D b) //returns -1 if vectors return negative dot product 
            {
                double check = a.Dot(b);
                if (check < 0)
                    return -1;
                else
                    return 1;
            }

            public static double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
            {
                if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
                    return 0;
                else
                    return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
            }

            /// <summary>
            /// Credit to unknown dude off the internet for this crazy code.
            /// </summary>
            /// <param name="X"></param>
            /// <param name="Y"></param>
            public static void ComputeCoefficients(double[,] X, double[] Y)
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
    }
}
