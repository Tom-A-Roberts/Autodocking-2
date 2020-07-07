using System;
using VRageMath;

namespace IngameScript
{
    internal partial class Program
    {
        /// <summary>
        ///     Whip's PID controller class v6 - 11/22/17<br />
        ///     Most of the code is thanks to Whip. It's used to do some essential maths when rotating the ship.
        /// </summary>
        public class PID
        {
            private readonly bool _integralDecay;
            private readonly double _integralDecayRatio;
            private readonly double _kD;
            private readonly double _kI;
            private readonly double _kP;
            private readonly double _lowerBound;
            private readonly double _upperBound;
            private double _errorSum;
            private bool _firstRun = true;
            private double _inverseTimeStep;
            private double _lastError;
            private double _timeStep;

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

            public double Value { get; private set; }


            public static Vector3D VectorProjection(Vector3D a, Vector3D b) //proj a on b    
            {
                var projection = a.Dot(b) / b.LengthSquared() * b;
                return projection;
            }

            public static int
                VectorCompareDirection(Vector3D a, Vector3D b) //returns -1 if vectors return negative dot product 
            {
                var check = a.Dot(b);
                if (check < 0)
                    return -1;
                return 1;
            }

            public static double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians 
            {
                if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
                    return 0;
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
            }

            public static Vector3D NearestPointOnLine(Vector3D linePoint, Vector3D lineDirection, Vector3D point)
            {
                var lineDir = Vector3D.Normalize(lineDirection);
                var v = point - linePoint;
                var d = v.Dot(lineDir);
                return linePoint + lineDir * d;
            }

            public static Vector3D ProjectPointOnPlane(Vector3D planeNormal, Vector3D planePoint, Vector3D point)
            {
                double distance;
                Vector3D translationVector;

                //First calculate the distance from the point to the plane:
                distance = SignedDistancePlanePoint(planeNormal, planePoint, point);

                //Reverse the sign of the distance
                distance *= -1;

                //Get a translation vector
                translationVector = SetVectorLength(planeNormal, distance);

                //Translate the point to form a projection
                return point + translationVector;
            }


            //Get the shortest distance between a point and a plane. The output is signed so it holds information
            //as to which side of the plane normal the point is.
            public static double SignedDistancePlanePoint(Vector3D planeNormal, Vector3D planePoint, Vector3D point)
            {
                return Vector3D.Dot(planeNormal, point - planePoint);
            }


            //create a vector of direction "vector" with length "size"
            public static Vector3D SetVectorLength(Vector3D vector, double size)
            {
                //normalize the vector
                var vectorNormalized = Vector3D.Normalize(vector);

                //scale the vector
                return vectorNormalized *= size;
            }


            /// <summary>
            ///     Credit to unknown dude off the internet for this crazy code.
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
                        if (X[I, K] != 0)
                        {
                            for (J = K1; J < N; J++) X[I, J] /= X[I, K];
                            Y[I] /= X[I, K];
                        }

                    for (I = K1; I < N; I++)
                        if (X[I, K] != 0)
                        {
                            for (J = K1; J < N; J++) X[I, J] -= X[K, J];
                            Y[I] -= Y[K];
                        }
                }

                for (I = N - 2; I >= 0; I--)
                for (J = N - 1; J >= I + 1; J--)
                    Y[I] -= X[I, J] * Y[J];
            }

            public static void Invert(ref Matrix3x3 matrix, out Matrix3x3 result)
            {
                var num = matrix.Determinant();
                var num2 = 1f / num;
                result.M11 = (matrix.M22 * matrix.M33 - matrix.M32 * matrix.M23) * num2;
                result.M12 = (matrix.M13 * matrix.M32 - matrix.M12 * matrix.M33) * num2;
                result.M13 = (matrix.M12 * matrix.M23 - matrix.M13 * matrix.M22) * num2;
                result.M21 = (matrix.M23 * matrix.M31 - matrix.M21 * matrix.M33) * num2;
                result.M22 = (matrix.M11 * matrix.M33 - matrix.M13 * matrix.M31) * num2;
                result.M23 = (matrix.M21 * matrix.M13 - matrix.M11 * matrix.M23) * num2;
                result.M31 = (matrix.M21 * matrix.M32 - matrix.M31 * matrix.M22) * num2;
                result.M32 = (matrix.M31 * matrix.M12 - matrix.M11 * matrix.M32) * num2;
                result.M33 = (matrix.M11 * matrix.M22 - matrix.M21 * matrix.M12) * num2;
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
                Value = _kP * error + _kI * _errorSum + _kD * errorDerivative;
                return Value;
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