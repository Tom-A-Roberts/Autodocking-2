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
        /// This class is used as a modularized section of code (for cleanliness) that handles all analysis of the ship's systems.<br />
        /// This includes finding the ship mass, main cockpit, connectors, thrusters, gyros etc.
        /// </summary>
        public class ShipSystemsAnalyzer
        {
            readonly Program parent_program;
            private bool firstTime = true;

            /// <summary>
            /// The main Cockpit of the ship, used to define mass, "Forwards" and other coordinate systems.
            /// </summary>
            public IMyShipController cockpit;

            public IMyShipConnector myConnector; // OUTDATED MUST CHANGE!


            public List<IMyThrust> thrusters = new List<IMyThrust>();
            public List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            public List<IMyGyro> gyros = new List<IMyGyro>();

            /// <summary>
            /// Mass of the ship in Kilograms
            /// </summary>
            public float shipMass = 9999;
            /// <summary>
            /// Mass of the ship in Kilograms, the last time the script was run. This is used to detect changes in the mass.
            /// </summary>
            public float previousShipMass = 9999;

            public ShipSystemsAnalyzer(Program _parent_program)
            {
                parent_program = _parent_program;
                Echo2("INITIALIZED\n");
                GatherBasicData();
            }

            /// <summary>
            /// Gathers the blocks that relate to the ship. This includes:<br />
            /// The cockpit, thrusters, gyros, (main) connector.
            /// </summary>
            public void GatherBasicData()
            {
                parent_program.GridTerminalSystem.GetBlocks(blocks);
                if (!firstTime && !scriptEnabled)
                {
                    Echo2("RE-INITIALIZED\nSome change was detected\nso I have re-checked ship data.");
                }
                

                cockpit = FindCockpit();
                if (cockpit != null)
                {
                    var Masses = cockpit.CalculateShipMass();
                    shipMass = Masses.PhysicalMass; //In kg
                    previousShipMass = shipMass;
                }
                else
                {
                    Error("The ship systems analyzer couldn't find some sort of cockpit or remote control.\nPlease check you have one of these, captain.");
                }

                myConnector = FindConnector();
                thrusters = FindThrusters();
                gyros = FindGyros();
                if (!parent_program.errorState)
                {
                    if (firstTime)
                    {
                        Echo2("Mass: " + shipMass.ToString());
                        Echo2("Thruster count: " + thrusters.Count.ToString());
                        Echo2("Gyro count: " + gyros.Count.ToString());
                        Echo2("Waiting for orders, Your Highness.");
                        EchoFinish(false);
                    }
                }
                firstTime = false;
            }


            /// <summary>
            /// When called, the ship will look through it's grid (And in-turn, it's connected grid) to find what connector the ship is connected to.<br />
            /// More specifically, it looks for a connector that is part of the cockpit's grid and is "connected".
            /// </summary>
            /// <returns>IMyShipConnector</returns>
            public IMyShipConnector FindMyConnectedConnector()
            {
                IMyShipConnector output = null;
                List<IMyShipConnector> Connectors = new List<IMyShipConnector>();
                parent_program.GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(Connectors);

                bool found_connected_connector = false;
                bool found_connectable_connector = false;
                foreach (var connector in Connectors)
                {
                    if (cockpit.CubeGrid.ToString() == connector.CubeGrid.ToString())
                    {
                        if (connector.Status == MyShipConnectorStatus.Connected)
                        {
                            if (!found_connected_connector)
                            {
                                found_connected_connector = true;
                                output = connector;
                            }
                        }
                        else if (connector.Status == MyShipConnectorStatus.Connectable)
                        {
                            if (found_connected_connector == false)
                            {
                                if (!found_connectable_connector)
                                {
                                    found_connectable_connector = true;
                                    output = connector;
                                }
                            }
                        }
                    }
                }
                return output;
            }

            public void CheckForMassChange()
            {
                var Masses =cockpit.CalculateShipMass();
                shipMass = Masses.PhysicalMass; //In kg
                if (previousShipMass != shipMass)
                {
                    GatherBasicData();
                }
                previousShipMass = shipMass;
            }

            IMyShipController FindCockpit()
            {
                List<IMyShipController> cockpits = new List<IMyShipController>();
                IMyShipController foundCockpit = null;
                bool foundMainCockpit = false;
                foreach (var block in blocks)
                {
                    if (block is IMyShipController && blockIsOnMyGrid(block))
                    {
                        if (foundCockpit == null)
                        {
                            foundCockpit = (IMyShipController)block;
                        }
                        if (block is IMyCockpit)
                        {
                            IMyCockpit c_cockpit = (IMyCockpit)block;
                            if (foundMainCockpit == false)
                            {
                                foundCockpit = (IMyShipController)block;
                            }
                            if (c_cockpit.IsMainCockpit == true)
                            {
                                foundMainCockpit = true;
                                foundCockpit = (IMyShipController)block;
                            }
                        }
                        if (block is IMyRemoteControl && foundMainCockpit == false)
                        {
                            foundCockpit = (IMyShipController)block;
                        }
                    }
                }
                return foundCockpit;
            }
            IMyShipConnector FindConnector()
            {

                IMyShipConnector foundConnector = null;
                foreach (var block in blocks)
                {
                    if (block is IMyShipConnector && blockIsOnMyGrid(block))
                    {
                        if (foundConnector == null)
                        {
                            foundConnector = (IMyShipConnector)block;
                        }
                        if (block.CustomName.ToLower().Contains("[dock]") == true)
                        {
                            foundConnector = (IMyShipConnector)block;
                        }
                    }
                }
                if (foundConnector == null)
                {
                    Error("I couldn't find a connector on this ship, Your Highness.");
                }
                return foundConnector;
            }

            /// <summary>
            /// Checks if a given block is on the same grid as the programming block
            /// </summary>
            /// <param name="block"></param>
            /// <returns>bool</returns>
            public bool blockIsOnMyGrid(IMyTerminalBlock block)
            {
                return block.CubeGrid.ToString() == parent_program.Me.CubeGrid.ToString();
            }

            List<IMyThrust> FindThrusters()
            {
                List<IMyThrust> o_thrusters = new List<IMyThrust>();

                foreach (var block in blocks)
                {
                    if (block is IMyThrust && block.IsWorking && blockIsOnMyGrid(block))
                    {

                        o_thrusters.Add((IMyThrust)block);
                    }
                }

                return o_thrusters;
            }

            List<IMyGyro> FindGyros()
            {
                List<IMyGyro> o_gyros = new List<IMyGyro>();
                foreach (var block in blocks)
                {
                    if (block is IMyGyro && block.IsWorking && blockIsOnMyGrid(block))
                    {
                        o_gyros.Add((IMyGyro)block);
                    }
                }
                return o_gyros;
            }


            #region Overrides
            private void Echo2(object inp)
            {
                parent_program.shipIOHandler.Echo(inp);
            }
            private void EchoFinish(bool OnlyInProgrammingBlock = false)
            {
                parent_program.shipIOHandler.EchoFinish(OnlyInProgrammingBlock);
            }
            private void Error(string str)
            {
                parent_program.shipIOHandler.Error(str);
            }
            #endregion
        }
    }
}
