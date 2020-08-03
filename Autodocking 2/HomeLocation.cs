using System.Collections.Generic;
using System.Xml.Serialization;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    internal partial class Program
    {
        /// <summary>
        ///     Each HomeLocation has it's own station connector and ship connector. <br />
        ///     A set of arguments are associated with each HomeLocation which when called, will return the ship to the
        ///     Homelocation.
        /// </summary>
        public class HomeLocation
        {
            private const char main_delimeter = '¬';
            private const char arg_delimeter = '`';
            
            public HashSet<string> arguments = new HashSet<string>();


            public IMyShipConnector shipConnector;
            public long shipConnectorID; // myConnector.EntityId;
            public Vector3D stationAcceleration = Vector3D.Zero;
            public Vector3D stationAngularVelocity = Vector3D.Zero;
            public Vector3D stationConnectorForward;
            public long stationConnectorID;
            public Vector3D stationConnectorLeft;
            public Vector3D stationConnectorPosition;



            public string stationConnectorName = null;
            public string stationGridName = null;

            //public Vector3D stationConnectorUpGlobal;
            //public Vector3D stationOriginalConnectorPosition;
            //public Vector3D stationOriginalConnectorForward;
            //public Vector3D stationOriginalConnectorUp;

            public double stationConnectorSize;

            // These are SAVED directions! NOT world matrix auxilleryDirection
            public Vector3D stationConnectorUpGlobal;
            public Vector3D stationConnectorUpLocal;
            public long stationGridID;

            public Vector3D stationVelocity = Vector3D.Zero;

            private const char landing_sequence_delimeter = 'ç';
            private const char waypoint_delimeter = 'å';
            private const char waypoint_data_delimeter = 'ã';

            public Dictionary<string, List<Waypoint>> landingSequences = new Dictionary<string, List<Waypoint>>();


            public void landingSequencesFromString(string data)
            {
                landingSequences = new Dictionary<string, List<Waypoint>>();
                if (data != "")
                {
                    string[] split_landing_sequences = data.Split(landing_sequence_delimeter);

                    foreach (string landingSequenceStr in split_landing_sequences)
                    {
                        if (landingSequenceStr != "")
                        {
                            string[] split_into_waypoints = landingSequenceStr.Split(waypoint_delimeter);
                            if (split_into_waypoints.Length > 1)
                            {
                                string current_arg = split_into_waypoints[0];
                                List<Waypoint> currentLandingSequence = new List<Waypoint>();
                                for (int i = 1; i < split_into_waypoints.Length; i++)
                                {
                                    if (split_into_waypoints[i] != "")
                                    {
                                        string[] waypoint_data = split_into_waypoints[i].Split(waypoint_data_delimeter);
                                        Vector3D pos = new Vector3D();
                                        Vector3D forward = new Vector3D();
                                        Vector3D up = new Vector3D();
                                        double waypoint_accuracy = 0.2;
                                        double top_speed = 1;
                                        bool require_rotation = true;
                                        Vector3D.TryParse(waypoint_data[0], out pos);
                                        Vector3D.TryParse(waypoint_data[1], out forward);
                                        Vector3D.TryParse(waypoint_data[2], out up);
                                        double.TryParse(waypoint_data[3], out top_speed);
                                        bool.TryParse(waypoint_data[4], out require_rotation);
                                        double.TryParse(waypoint_data[5], out waypoint_accuracy);
                                        Waypoint newWaypoint = new Waypoint(pos, forward, up);
                                        newWaypoint.WaypointIsLocal = true;
                                        newWaypoint.waypoint_completion_accuracy = waypoint_accuracy;
                                        newWaypoint.top_speed = top_speed;
                                        newWaypoint.RequireRotation = require_rotation;
                                        currentLandingSequence.Add(newWaypoint);
                                    }
                                }
                                if (currentLandingSequence.Count > 0)
                                {
                                    landingSequences[current_arg] = currentLandingSequence;
                                }
                            }
                        }
                    }

                }
            }
            public string landingSequencesToString()
            {
                string o_string = "";

                foreach (KeyValuePair<string, List<Waypoint>> landingSequenceEntry in landingSequences)
                {
                    string current_landing_sequence_string = landingSequenceEntry.Key + waypoint_delimeter;
                    foreach (Waypoint waypoint in landingSequenceEntry.Value)
                    {
                        string waypoint_representation = "";
                        waypoint_representation += waypoint.position.ToString() + waypoint_data_delimeter;
                        waypoint_representation += waypoint.forward.ToString() + waypoint_data_delimeter;
                        waypoint_representation += waypoint.auxilleryDirection.ToString() + waypoint_data_delimeter;
                        waypoint_representation += waypoint.top_speed.ToString() + waypoint_data_delimeter;
                        waypoint_representation += waypoint.RequireRotation.ToString() + waypoint_data_delimeter;
                        waypoint_representation += waypoint.waypoint_completion_accuracy.ToString();
                        current_landing_sequence_string += waypoint_representation + waypoint_delimeter;
                    }
                    o_string += current_landing_sequence_string + landing_sequence_delimeter;
                }
                return o_string;
            }

            public void ExtractSavedNames(string data_string)
            {
                string[] data_split = data_string.Split(waypoint_delimeter);
                stationGridName = data_split[0];
                stationConnectorName = data_split[1];
            }

            public string ProduceSavedNames()
            {
                string o_string = "";
                o_string += stationGridName + waypoint_delimeter;
                o_string += stationConnectorName;
                return o_string;
            }



            /// <summary>
            ///     new_arg = the initial argument to be associated with this HomeLocation<br />
            ///     my_connector = the connector on the ship<br />
            ///     station_connector = the connector on the station
            /// </summary>
            /// <param name="new_arg"></param>
            /// <param name="my_connector"></param>
            /// <param name="station_connector"></param>
            public HomeLocation(string new_arg, IMyShipConnector my_connector, IMyShipConnector station_connector)
            {
                arguments.Add(new_arg);
                UpdateData(my_connector, station_connector);
            }

            public HomeLocation(string saved_data_string, Program parent_program)
            {
                var data_parts = saved_data_string.Split(main_delimeter);
                if (data_parts.Length == 8)
                {
                    long.TryParse(data_parts[0], out shipConnectorID);
                    long.TryParse(data_parts[1], out stationConnectorID);
                    Vector3D.TryParse(data_parts[2], out stationConnectorPosition);
                    Vector3D.TryParse(data_parts[3], out stationConnectorForward);
                    Vector3D.TryParse(data_parts[4], out stationConnectorUpGlobal);
                    long.TryParse(data_parts[5], out stationGridID);
                    double.TryParse(data_parts[6], out stationConnectorSize);
                    var argument_parts = data_parts[7].Split(arg_delimeter);
                    foreach (var arg in argument_parts) arguments.Add(arg);
                    UpdateShipConnectorUsingID(parent_program);
                }
                else if (data_parts.Length == 12)
                {
                    long.TryParse(data_parts[0], out shipConnectorID);
                    long.TryParse(data_parts[1], out stationConnectorID);
                    Vector3D.TryParse(data_parts[2], out stationConnectorPosition);
                    Vector3D.TryParse(data_parts[3], out stationConnectorForward);
                    Vector3D.TryParse(data_parts[4], out stationConnectorUpGlobal);
                    Vector3D.TryParse(data_parts[5], out stationConnectorUpLocal);
                    Vector3D.TryParse(data_parts[6], out stationConnectorLeft);
                    long.TryParse(data_parts[7], out stationGridID);
                    double.TryParse(data_parts[8], out stationConnectorSize);
                    landingSequencesFromString(data_parts[9]);
                    ExtractSavedNames(data_parts[10]);
                    var argument_parts = data_parts[11].Split(arg_delimeter);
                    foreach (var arg in argument_parts) arguments.Add(arg);
                    UpdateShipConnectorUsingID(parent_program);
                }
                else
                {
                    shipConnector = null;
                }
            }

            /// <summary>
            ///     Serializes the HomeLocation into a string ready to be saved.
            /// </summary>
            /// <returns></returns>
            public string ProduceSaveData()
            {
                var o_string = "";
                o_string += shipConnectorID.ToString() + main_delimeter;
                o_string += stationConnectorID.ToString() + main_delimeter;
                o_string += stationConnectorPosition.ToString() + main_delimeter;
                o_string += stationConnectorForward.ToString() + main_delimeter;
                o_string += stationConnectorUpGlobal.ToString() + main_delimeter;
                o_string += stationConnectorUpLocal.ToString() + main_delimeter;
                o_string += stationConnectorLeft.ToString() + main_delimeter;

                //if(stationGridID.Contains(";") || stationGridID.Contains(main_delimeter) || stationGridID.Contains("#"))
                //{
                //    stationGridID = "Name contained bad char";
                //}
                o_string += stationGridID.ToString() + main_delimeter;
                o_string += stationConnectorSize.ToString() + main_delimeter;

                o_string += landingSequencesToString() + main_delimeter;
                //Matrix transformMat = Matrix.

                o_string += ProduceSavedNames() + main_delimeter;

                foreach (var arg in arguments) o_string += arg + arg_delimeter;
                o_string = o_string.Substring(0, o_string.Length - 1);

                return o_string;
            }

            public string ProduceUserFriendlyData()
            {
                var o_string = "";

                foreach (var arg in arguments)
                {
                    o_string += stationGridName + ";";
                    o_string += stationConnectorName + ";";
                    o_string += shipConnector.CustomName + ";";
                    o_string += arg + ";";
                    int waypointCount = 0;
                    if (landingSequences.ContainsKey(arg))
                    {
                        waypointCount = landingSequences[arg].Count;
                    }

                    o_string += waypointCount.ToString();
                    o_string += "\n";

                }

                if (o_string.Length > 1)
                {
                    o_string = o_string.Substring(0, o_string.Length - 1);
                }
                

                return o_string;
            }

            /// <summary>
            ///     This method will update the HomeLocation information about the station connector position and orientation.<br />
            ///     Requires the ship to be connected.
            /// </summary>
            /// <param name="my_connector"></param>
            /// <param name="station_connector"></param>
            public void UpdateData(IMyShipConnector my_connector, IMyShipConnector station_connector)
            {
                shipConnectorID = my_connector.EntityId;
                shipConnector = my_connector;
                stationConnectorID = station_connector.EntityId;
                stationConnectorPosition = station_connector.GetPosition();
                stationConnectorForward = station_connector.WorldMatrix.Forward;
                stationConnectorLeft = station_connector.WorldMatrix.Left;

                stationConnectorName = station_connector.CustomName;
                stationGridName = station_connector.CubeGrid.CustomName;

                var normalizedleft = Vector3D.Normalize(PID.ProjectPointOnPlane(stationConnectorForward, Vector3D.Zero,
                    my_connector.WorldMatrix.Left));
                var saved_up = normalizedleft.Cross(stationConnectorForward);
                stationConnectorUpGlobal = saved_up;
                stationConnectorUpLocal = worldDirectionToLocalDirection(stationConnectorUpGlobal, station_connector.WorldMatrix);


                stationGridID = station_connector.CubeGrid.EntityId;
                stationConnectorSize = ShipSystemsAnalyzer.GetRadiusOfConnector(station_connector);
            }

            public static Vector3D worldDirectionToLocalDirection(Vector3D world_direction, MatrixD world_matrix)
            {
                return Vector3D.TransformNormal(world_direction, MatrixD.Transpose(world_matrix));
            }

            public static Vector3D localDirectionToWorldDirection(Vector3D local_direction, MatrixD world_matrix)
            {
                return Vector3D.TransformNormal(local_direction, world_matrix);
            }
            public static Vector3D localDirectionToWorldDirection(Vector3D local_direction, HomeLocation referenceHomeLocation)
            {
                MatrixD newWorldMatrix = Matrix.CreateWorld(referenceHomeLocation.stationConnectorPosition, referenceHomeLocation.stationConnectorForward,
                    (-referenceHomeLocation.stationConnectorLeft).Cross(referenceHomeLocation.stationConnectorForward));
                return Vector3D.TransformNormal(local_direction, newWorldMatrix); ;
            }


            public static Vector3D localPositionToWorldPosition(Vector3D local_position, MatrixD world_matrix)
            {
                return Vector3D.Transform(local_position, world_matrix);
            }
            public static Vector3D localPositionToWorldPosition(Vector3D local_position, HomeLocation referenceHomeLocation)
            {
                MatrixD newWorldMatrix = Matrix.CreateWorld(referenceHomeLocation.stationConnectorPosition, referenceHomeLocation.stationConnectorForward,
                    (-referenceHomeLocation.stationConnectorLeft).Cross(referenceHomeLocation.stationConnectorForward));
                return Vector3D.Transform(local_position, newWorldMatrix);
            }
            public static Vector3D worldPositionToLocalPosition(Vector3D world_position, MatrixD world_matrix)
            {
                Vector3D worldDirection = world_position - world_matrix.Translation;

                return Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(world_matrix));
            }



            //public double angleFromVectors(Vector3D )
            //{

            //}

            public void UpdateShipConnectorUsingID(Program parent_program)
            {
                shipConnector = (IMyShipConnector) parent_program.GridTerminalSystem.GetBlockWithId(shipConnectorID);
            }

            public string UpdateDataFromOptionalHomeScript(string[] data_parts)
            {
                var issuestring = "";
                if (data_parts.Length == 6)
                {
                    // THEREFORE SCRIPT IS OLD
                    Vector3D.TryParse(data_parts[0], out stationConnectorPosition);
                    Vector3D.TryParse(data_parts[1], out stationConnectorForward);
                    Vector3D.TryParse(data_parts[2], out stationConnectorLeft);
                    Vector3D.TryParse(data_parts[3], out stationVelocity);
                    Vector3D.TryParse(data_parts[4], out stationAngularVelocity);
                    Vector3D.TryParse(data_parts[5], out stationAcceleration);

                    if (stationConnectorUpLocal != Vector3D.Zero)
                    {
                        MatrixD newWorldMatrix = Matrix.CreateWorld(stationConnectorPosition, stationConnectorForward,
                            (-stationConnectorLeft).Cross(stationConnectorForward));
                        var newConnectorUpGlobal =
                            localDirectionToWorldDirection(stationConnectorUpLocal, newWorldMatrix);
                        stationConnectorUpGlobal = newConnectorUpGlobal;
                    }
                }
                else
                {
                    issuestring =
                        "Warning:\nGot a corrupted message back from the optional home script.\nMaybe it is an old version?";
                }

                return issuestring;
            }

            /// <summary>
            ///     To test if two home locations are equal, both the station and ship connector ID's must match.
            /// </summary>
            /// <param name="obj"></param>
            /// <returns></returns>
            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType()) return false;

                var test = (HomeLocation) obj;
                if (shipConnectorID == test.shipConnectorID && stationConnectorID == test.stationConnectorID)
                    return true;
                return false;
            }

            /// <summary>
            ///     Calculates the Hash Code of the HomeLocation based upon the arguments and two connector ID's.
            /// </summary>
            /// <returns></returns>
            public override int GetHashCode()
            {
                var hashCode = -48872655;
                hashCode = hashCode * -1521134295 + EqualityComparer<HashSet<string>>.Default.GetHashCode(arguments);
                hashCode = hashCode * -1521134295 + shipConnectorID.GetHashCode();
                hashCode = hashCode * -1521134295 + stationConnectorID.GetHashCode();
                // hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode (station_connector_name);
                // hashCode = hashCode * -1521134295 + EqualityComparer<Vector3D>.Default.GetHashCode (station_connector_position);
                // hashCode = hashCode * -1521134295 + EqualityComparer<Vector3D>.Default.GetHashCode (station_connector_forward);
                // hashCode = hashCode * -1521134295 + EqualityComparer<Vector3D>.Default.GetHashCode (station_connector_up);
                return hashCode;
            }
        }
    }
}