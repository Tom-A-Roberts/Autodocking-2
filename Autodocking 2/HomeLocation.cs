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
            public string stationConnectorName;
            public Vector3D stationConnectorPosition;
            public Vector3D stationConnectorForward;
            public Vector3D stationConnectorUp;
            public double stationConnectorSize;

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

                if (data_parts.Length != 8)
                {
                    shipConnector = null;
                }
                else
                {
                    long.TryParse(data_parts[0], out shipConnectorID);
                    long.TryParse(data_parts[1], out stationConnectorID);
                    Vector3D.TryParse(data_parts[2], out stationConnectorPosition);
                    Vector3D.TryParse(data_parts[3], out stationConnectorForward);
                    Vector3D.TryParse(data_parts[4], out stationConnectorUp);
                    stationConnectorName = data_parts[5];
                    double.TryParse(data_parts[6], out stationConnectorSize);

                    string[] argument_parts = data_parts[7].Split(arg_delimeter);
                    foreach (string arg in argument_parts)
                    {
                        arguments.Add(arg);
                    }
                    UpdateShipConnectorUsingID(parent_program);
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
                o_string += stationConnectorUp.ToString() + main_delimeter;
                if(stationConnectorName.Contains(";") || stationConnectorName.Contains(main_delimeter) || stationConnectorName.Contains("#"))
                {
                    stationConnectorName = "Name contained bad char";
                }
                o_string += stationConnectorName + main_delimeter;
                o_string += stationConnectorSize.ToString() + main_delimeter;

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
                stationConnectorUp = station_connector.WorldMatrix.Up;
                stationConnectorName = station_connector.CustomName;
                stationConnectorSize = ShipSystemsAnalyzer.GetRadiusOfConnector(station_connector);

            }

            public void UpdateShipConnectorUsingID(Program parent_program)
            {
                shipConnector = (Sandbox.ModAPI.Ingame.IMyShipConnector)parent_program.GridTerminalSystem.GetBlockWithId(shipConnectorID);
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
