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
        /// Each HomeLocation has it's own station connector and ship connector. <br />
        /// A set of arguments are associated with each HomeLocation which when called, will return the ship to the Homelocation.
        /// </summary>
        public class HomeLocation
        {
            public HashSet<string> arguments = new HashSet<string>();
            public long my_connector_ID; // myConnector.EntityId;
            public long station_connector_ID;
            public string station_connector_name;
            public Vector3D station_connector_position;
            public Vector3D station_connector_forward;
            public Vector3D station_connector_up;

            /// <summary>
            /// new_arg = the initial argument to be associated with this HomeLocation<br />
            /// my_connector = the connector on the ship<br />
            /// station_connector = the connector on the station
            /// </summary>
            /// <param name="new_arg"></param>
            /// <param name="my_connector"></param>
            /// <param name="station_connector"></param>
            public HomeLocation(string new_arg, IMyShipConnector my_connector, IMyShipConnector station_connector)
            {
                arguments.Add(new_arg);
                my_connector_ID = my_connector.EntityId;
                station_connector_ID = station_connector.EntityId;
                station_connector_position = station_connector.GetPosition();
                station_connector_forward = station_connector.WorldMatrix.Up;
                station_connector_up = -station_connector.WorldMatrix.Forward;
                station_connector_name = station_connector.CustomName;
            }
            /// <summary>
            /// Serializes the HomeLocation into a string ready to be saved.
            /// </summary>
            /// <returns></returns>
            public string ProduceSaveData()
            {
                return null;
            }
            /// <summary>
            /// This method will update the HomeLocation information about the station connector position and orientation.<br />
            /// Requires the ship to be connected.
            /// </summary>
            /// <param name="my_connector"></param>
            /// <param name="station_connector"></param>
            public void UpdateData(IMyShipConnector my_connector, IMyShipConnector station_connector)
            {
                my_connector_ID = my_connector.EntityId;
                station_connector_ID = station_connector.EntityId;
                station_connector_position = station_connector.GetPosition();
                station_connector_forward = station_connector.WorldMatrix.Up;
                station_connector_up = -station_connector.WorldMatrix.Forward;
                station_connector_name = station_connector.CustomName;
            }
            /// <summary>
            /// To test if two home locations are equal, both the station and ship connector ID's must match.
            /// </summary>
            /// <param name="obj"></param>
            /// <returns></returns>
            public override bool Equals(Object obj)
            {
                if ((obj == null) || this.GetType() == obj.GetType())
                {
                    return false;
                }
                else
                {
                    HomeLocation test = (HomeLocation)obj;
                    if (my_connector_ID == test.my_connector_ID && station_connector_ID == test.station_connector_ID)
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
                hashCode = hashCode * -1521134295 + my_connector_ID.GetHashCode();
                hashCode = hashCode * -1521134295 + station_connector_ID.GetHashCode();
                // hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode (station_connector_name);
                // hashCode = hashCode * -1521134295 + EqualityComparer<Vector3D>.Default.GetHashCode (station_connector_position);
                // hashCode = hashCode * -1521134295 + EqualityComparer<Vector3D>.Default.GetHashCode (station_connector_forward);
                // hashCode = hashCode * -1521134295 + EqualityComparer<Vector3D>.Default.GetHashCode (station_connector_up);
                return hashCode;
            }
        }



    }
}
