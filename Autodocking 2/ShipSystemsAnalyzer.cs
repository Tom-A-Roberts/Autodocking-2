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
            public Program parent_program;
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

            public struct ThrusterForceAnalysis
            {
                ShipSystemsAnalyzer systems_parent;

                // Thruster groups
                public List<IMyThrust> ForceForwardThrusters; // Forward with respect to the direction the thrusters will be firing
                public List<IMyThrust> ForceLeftThrusters;
                public List<IMyThrust> ForceUpThrusters;
                public List<IMyThrust> UnusedThrusters;
                // Accumulated max thrust numbers
                public double ForwardMaxThrust;
                public double LeftMaxThrust;
                public double UpMaxThrust;
                // Thrust directions
                public Vector3D thrustForward;
                public Vector3D thrustLeft;
                public Vector3D thrustUp;

                public IMyTerminalBlock blockWithRespectTo;
                public Vector3D analysisDirection;


                public ThrusterForceAnalysis(Vector3D directionToAnalyze, IMyTerminalBlock _blockWithRespectTo, ShipSystemsAnalyzer _systems_parent)
                {
                    blockWithRespectTo = _blockWithRespectTo;
                    systems_parent = _systems_parent;
                    analysisDirection = directionToAnalyze;

                    
                    ForceForwardThrusters = new List<IMyThrust>();
                    ForceLeftThrusters = new List<IMyThrust>();
                    ForceUpThrusters = new List<IMyThrust>();
                    UnusedThrusters = new List<IMyThrust>();



                    ForwardMaxThrust = 0;
                    LeftMaxThrust = 0;
                    UpMaxThrust = 0;


                    var referenceOrigin = blockWithRespectTo.GetPosition();

                    var block_WorldMatrix = VRageMath.Matrix.CreateWorld(referenceOrigin,
                        blockWithRespectTo.WorldMatrix.Up, //referenceBlock.WorldMatrix.Forward,
                        -blockWithRespectTo.WorldMatrix.Forward //referenceBlock.WorldMatrix.Up
                    );

                    thrustForward = block_WorldMatrix.Forward;
                    thrustLeft = block_WorldMatrix.Left;
                    thrustUp = block_WorldMatrix.Up;

                    if (thrustForward.Dot(analysisDirection) < 0)
                    {
                        thrustForward *= -1;
                    }
                    if (thrustLeft.Dot(analysisDirection) < 0)
                    {
                        thrustLeft *= -1;
                    }
                    if (thrustUp.Dot(analysisDirection) < 0)
                    {
                        thrustUp *= -1;
                    }

                    foreach (IMyThrust thisThruster in systems_parent.thrusters)
                    {
                        var thrusterDirection = -thisThruster.WorldMatrix.Forward;
                        double forwardDot = Vector3D.Dot(thrusterDirection, Vector3D.Normalize(thrustForward));
                        double leftDot = Vector3D.Dot(thrusterDirection, Vector3D.Normalize(thrustLeft));
                        double upDot = Vector3D.Dot(thrusterDirection, Vector3D.Normalize(thrustUp));

                        if (forwardDot >= 0.97)
                        {
                            ForceForwardThrusters.Add(thisThruster);
                        }
                        else if (leftDot >= 0.97)
                        {
                            ForceLeftThrusters.Add(thisThruster);
                        }
                        else if (upDot >= 0.97)
                        {
                            ForceUpThrusters.Add(thisThruster);
                        }
                        else
                        {
                            //thisThruster.ThrustOverride = 0f; // WARNING THRUSTER CONTROL HERE!
                            UnusedThrusters.Add(thisThruster);
                        }
                    }

                    foreach (IMyThrust thisThruster in ForceForwardThrusters)
                    {
                        ForwardMaxThrust += thisThruster.MaxEffectiveThrust;
                    }
                    foreach (IMyThrust thisThruster in ForceLeftThrusters)
                    {
                        LeftMaxThrust += thisThruster.MaxEffectiveThrust;
                    }
                    foreach (IMyThrust thisThruster in ForceUpThrusters)
                    {
                        UpMaxThrust += thisThruster.MaxEffectiveThrust;
                    }



                }
            }



            public ShipSystemsAnalyzer(Program in_parent_program)
            {
                parent_program = in_parent_program;
                parent_program.shipIOHandler.Echo("INITIALIZED\n");
                GatherBasicData();
            }





            #region Methods


            /// <summary>
            /// Gathers the blocks that relate to the ship. This includes:<br />
            /// The cockpit, thrusters, gyros, (main) connector.
            /// </summary>
            public void GatherBasicData()
            {
                parent_program.GridTerminalSystem.GetBlocks(blocks);
                if (!firstTime && !parent_program.scriptEnabled)
                {
                    parent_program.shipIOHandler.Echo("RE-INITIALIZED\nSome change was detected\nso I have re-checked ship data.");
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
                    parent_program.shipIOHandler.Error("The ship systems analyzer couldn't find some sort of cockpit or remote control.\nPlease check you have one of these, captain.");
                }

                myConnector = FindConnector();
                thrusters = FindThrusters();
                gyros = FindGyros();
                if (!parent_program.errorState)
                {
                    if (firstTime)
                    {
                        parent_program.shipIOHandler.Echo("Mass: " + shipMass.ToString());
                        parent_program.shipIOHandler.Echo("Thruster count: " + thrusters.Count.ToString());
                        parent_program.shipIOHandler.Echo("Gyro count: " + gyros.Count.ToString());
                        parent_program.shipIOHandler.Echo("Waiting for orders, Your Highness.");
                        parent_program.shipIOHandler.EchoFinish(false);
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

            #endregion

            #region Overrides
            private void Echo(object inp)
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
