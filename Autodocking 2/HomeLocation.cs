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
using Sandbox.ModAPI;

namespace IngameScript
{
    partial class Program
    {

        /// <summary>
        /// Each HomeLocation has it's own station connector and ship connector. <br />
        /// A set of arguments are associated with each HomeLocation which when called, will return the ship to the Homelocation.
        /// </summary>
        public class HomeLocation
        {
            public HashSet<string> arguments = new HashSet<string>();
            public long shipConnectorID; // myConnector.EntityId;
            public Sandbox.ModAPI.Ingame.IMyShipConnector shipConnector;
            public long stationConnectorID;
            public long stationGridID;
            public Vector3D stationConnectorPosition;
            public Vector3D stationConnectorForward;
            public Vector3D stationConnectorLeft;

            // These are SAVED directions! NOT world matrix up
            public Vector3D stationConnectorUpGlobal;
            public Vector3D stationConnectorUpLocal;

            
            //public Vector3D stationConnectorUpGlobal;
            //public Vector3D stationOriginalConnectorPosition;
            //public Vector3D stationOriginalConnectorForward;
            //public Vector3D stationOriginalConnectorUp;

            public double stationConnectorSize;

            public Vector3D stationVelocity = Vector3D.Zero;
            public Vector3D stationAngularVelocity = Vector3D.Zero;
            public Vector3D stationAcceleration = Vector3D.Zero;

            /// <summary>
            /// new_arg = the initial argument to be associated with this HomeLocation<br />
            /// my_connector = the connector on the ship<br />
            /// station_connector = the connector on the station
            /// </summary>
            /// <param name="new_arg"></param>
            /// <param name="my_connector"></param>
            /// <param name="station_connector"></param>
            public HomeLocation(string new_arg, Sandbox.ModAPI.Ingame.IMyShipConnector my_connector, Sandbox.ModAPI.Ingame.IMyShipConnector station_connector)
            {
                arguments.Add(new_arg);
                UpdateData(my_connector, station_connector);
            }
            const char main_delimeter = '¬';
            const char arg_delimeter = '`';


            public HomeLocation(string saved_data_string, Program parent_program)
            {

                string[] data_parts = saved_data_string.Split(main_delimeter);
                if(data_parts.Length == 8)
                {
                    long.TryParse(data_parts[0], out shipConnectorID);
                    long.TryParse(data_parts[1], out stationConnectorID);
                    Vector3D.TryParse(data_parts[2], out stationConnectorPosition);
                    Vector3D.TryParse(data_parts[3], out stationConnectorForward);
                    Vector3D.TryParse(data_parts[4], out stationConnectorUpGlobal);
                    long.TryParse(data_parts[5], out stationGridID);
                    double.TryParse(data_parts[6], out stationConnectorSize);
                    string[] argument_parts = data_parts[7].Split(arg_delimeter);
                    foreach (string arg in argument_parts)
                    {
                        arguments.Add(arg);
                    }
                    UpdateShipConnectorUsingID(parent_program);
                }
                else if(data_parts.Length == 11)
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

                    string[] argument_parts = data_parts[10].Split(arg_delimeter);
                    foreach (string arg in argument_parts)
                    {
                        arguments.Add(arg);
                    }
                    UpdateShipConnectorUsingID(parent_program);
                }
                else
                {
                    shipConnector = null;
                }
            }

            /// <summary>
            /// Serializes the HomeLocation into a string ready to be saved.
            /// </summary>
            /// <returns></returns>
            public string ProduceSaveData()
            {
                string o_string = "";
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

                o_string += "NULL for future update" + main_delimeter;
                //Matrix transformMat = Matrix.

                foreach (string arg in arguments)
                {
                    o_string += arg + arg_delimeter;
                }o_string = o_string.Substring(0, o_string.Length - 1);

                return o_string;
            }

            /// <summary>
            /// This method will update the HomeLocation information about the station connector position and orientation.<br />
            /// Requires the ship to be connected.
            /// </summary>
            /// <param name="my_connector"></param>
            /// <param name="station_connector"></param>
            public void UpdateData(Sandbox.ModAPI.Ingame.IMyShipConnector my_connector, Sandbox.ModAPI.Ingame.IMyShipConnector station_connector)
            {
                shipConnectorID = my_connector.EntityId;
                shipConnector = my_connector;
                stationConnectorID = station_connector.EntityId;
                stationConnectorPosition = station_connector.GetPosition();
                stationConnectorForward = station_connector.WorldMatrix.Forward;
                stationConnectorLeft = station_connector.WorldMatrix.Left;

                Vector3D normalizedleft = Vector3D.Normalize(PID.ProjectPointOnPlane(stationConnectorForward, Vector3D.Zero, my_connector.WorldMatrix.Left));
                Vector3D saved_up = normalizedleft.Cross(stationConnectorForward);
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

            //public double angleFromVectors(Vector3D )
            //{

            //}

            public void UpdateShipConnectorUsingID(Program parent_program)
            {
                shipConnector = (Sandbox.ModAPI.Ingame.IMyShipConnector)parent_program.GridTerminalSystem.GetBlockWithId(shipConnectorID);
            }

            public string UpdateDataFromOptionalHomeScript(string[] data_parts)
            {
                string issuestring = "";
                if (data_parts.Length == 6)
                {
                    // THEREFORE SCRIPT IS OLD
                    Vector3D.TryParse(data_parts[0], out stationConnectorPosition);
                    Vector3D.TryParse(data_parts[1], out stationConnectorForward);
                    Vector3D.TryParse(data_parts[2], out stationConnectorLeft);
                    Vector3D.TryParse(data_parts[3], out stationVelocity);
                    Vector3D.TryParse(data_parts[4], out stationAngularVelocity);
                    Vector3D.TryParse(data_parts[5], out stationAcceleration);

                    if(stationConnectorUpLocal != Vector3D.Zero)
                    {
                        MatrixD newWorldMatrix = VRageMath.Matrix.CreateWorld(stationConnectorPosition, stationConnectorForward, (-stationConnectorLeft).Cross(stationConnectorForward));
                        Vector3D newConnectorUpGlobal = localDirectionToWorldDirection(stationConnectorUpLocal, newWorldMatrix);
                        stationConnectorUpGlobal = newConnectorUpGlobal;
                    }
                }
                else
                {
                    issuestring = "Warning:\nGot a corrupted message back from the optional home script.\nMaybe it is an old version?";
                }
                return issuestring;
            }

            /// <summary>
            /// To test if two home locations are equal, both the station and ship connector ID's must match.
            /// </summary>
            /// <param name="obj"></param>
            /// <returns></returns>
            public override bool Equals(Object obj)
            {
                if ((obj == null) || this.GetType() != obj.GetType())
                {
                    return false;
                }
                else
                {
                    HomeLocation test = (HomeLocation)obj;
                    if (shipConnectorID == test.shipConnectorID && stationConnectorID == test.stationConnectorID)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            /// <summary>
            /// Calculates the Hash Code of the HomeLocation based upon the arguments and two connector ID's.
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
