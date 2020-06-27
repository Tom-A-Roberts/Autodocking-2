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
using VRage.Noise.Modifiers;
using System.Security.Permissions;

namespace IngameScript
{
    partial class Program
    {
        /// <summary>This class is used as a modularized section of code (for cleanliness) that handles all analysis of the ship's systems.<br />This includes finding the ship mass, main cockpit, connectors, thrusters, gyros etc.</summary>
        public class ShipSystemsAnalyzer
        {
            public Program parent_program;
            private bool firstTime = true;

            /// <summary>The main Cockpit of the ship, used to define mass, "Forwards" and other coordinate systems.</summary>
            public IMyShipController cockpit;

            //Current status variables:
            //public IMyShipConnector myConnector; // OUTDATED MUST CHANGE!
            public HomeLocation currentHomeLocation;

            public List<IMyThrust> thrusters = new List<IMyThrust>();
            public List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            public List<IMyGyro> gyros = new List<IMyGyro>();

            /// <summary>Mass of the ship in Kilograms</summary>
            public float shipMass = 9999;
            /// <summary>Mass of the ship in Kilograms, the last time the script was run. This is used to detect changes in the mass.</summary>
            public float previousShipMass = 9999;

            public bool basicDataGatherRequired = false;

            /// <summary>Directions are with respect to the cockpit.</summary>
            public ThrusterGroup ForwardThrust;
            public ThrusterGroup UpThrust;
            public ThrusterGroup LeftThrust;
            public ThrusterGroup BackwardThrust;
            public ThrusterGroup DownThrust;
            public ThrusterGroup RightThrust;
            public Dictionary<Base6Directions.Direction,ThrusterGroup> thrusterGroups;


            public ShipSystemsAnalyzer(Program in_parent_program)
            {
                parent_program = in_parent_program;
                parent_program.shipIOHandler.Echo("INITIALIZED");
                GatherBasicData();
            }

            /// <summary>
            /// Solves the required thrusts to go in targetDirection at max speed. This takes gravity into account.
            /// </summary>
            /// <param name="g">Gravity and unknown forces</param>
            /// <param name="targetDirection">The target direction to go in</param>
            /// <param name="maxPercentageThrustToUse">The maximum amount of thrust devoted to going in the target direction.</param>
            /// <returns></returns>
            public ThrusterGroup SolveMaxThrust(Vector3D minus_g, Vector3D targetDirection, double maxPercentageThrustToUse = 1)
            {
                //parent_program.Echo("Starting");
                Base6Directions.Direction actual2Di;
                Base6Directions.Direction actual3Di;
                double t2c;
                double t3c;
                double Lambda;
                ThrusterGroup t1;
                ThrusterGroup t2;
                ThrusterGroup t3;
                if (maxPercentageThrustToUse > 1)
                {
                    maxPercentageThrustToUse = 1;
                }else if(maxPercentageThrustToUse < 0)
                {
                    maxPercentageThrustToUse = 0;
                }
                foreach (KeyValuePair<Base6Directions.Direction, ThrusterGroup> entry in thrusterGroups)
                {
                    //parent_program.Echo("Loading " + entry.Key.ToString());

                    t1 = entry.Value;

                    if (t1.LocalThrustDirection == Base6Directions.Direction.Up || t1.LocalThrustDirection == Base6Directions.Direction.Down)
                    {
                        t2 = thrusterGroups[Base6Directions.Direction.Left];
                        t3 = thrusterGroups[Base6Directions.Direction.Forward];
                    }
                    else if (t1.LocalThrustDirection == Base6Directions.Direction.Left || t1.LocalThrustDirection == Base6Directions.Direction.Right)
                    {
                        t2 = thrusterGroups[Base6Directions.Direction.Up];
                        t3 = thrusterGroups[Base6Directions.Direction.Forward];
                    }
                    else if (t1.LocalThrustDirection == Base6Directions.Direction.Forward || t1.LocalThrustDirection == Base6Directions.Direction.Backward)
                    {
                        t2 = thrusterGroups[Base6Directions.Direction.Up];
                        t3 = thrusterGroups[Base6Directions.Direction.Left];
                    }
                    else
                    {
                        parent_program.shipIOHandler.Error("Encountered unusual thruster direction.\nIf you've gotten this error in particular,\nplease report this error to the script owner, Spug.");
                        t2 = thrusterGroups[Base6Directions.Direction.Up];
                        t3 = thrusterGroups[Base6Directions.Direction.Left];
                    }

                    //var mat = new double[,] {
                    //{ t2.WorldThrustDirection.X, t3.WorldThrustDirection.X, -targetDirection.X },
                    //{ t2.WorldThrustDirection.Y, t3.WorldThrustDirection.Y, -targetDirection.Y },
                    //{ t2.WorldThrustDirection.Z, t3.WorldThrustDirection.Z, -targetDirection.Z },
                    //};
                    t1.matrixM[0, 0] = t2.WorldThrustDirection.X;
                    t1.matrixM[0, 1] = t3.WorldThrustDirection.X;
                    t1.matrixM[0, 2] = -targetDirection.X;

                    t1.matrixM[1, 0] = t2.WorldThrustDirection.Y;
                    t1.matrixM[1, 1] = t3.WorldThrustDirection.Y;
                    t1.matrixM[1, 2] = -targetDirection.Y;

                    t1.matrixM[2, 0] = t2.WorldThrustDirection.Z;
                    t1.matrixM[2, 1] = t3.WorldThrustDirection.Z;
                    t1.matrixM[2, 2] = -targetDirection.Z;


                    t1.ANS[0] = (-t1.MaxThrust * t1.WorldThrustDirection.X * maxPercentageThrustToUse) - minus_g.X;
                    t1.ANS[1] = (-t1.MaxThrust * t1.WorldThrustDirection.Y * maxPercentageThrustToUse) - minus_g.Y;
                    t1.ANS[2] = (-t1.MaxThrust * t1.WorldThrustDirection.Z * maxPercentageThrustToUse) - minus_g.Z;

                    PID.ComputeCoefficients(t1.matrixM, t1.ANS);

                    t2c = t1.ANS[0];
                    t3c = t1.ANS[1];
                    Lambda = t1.ANS[2];

                    actual2Di = t2.LocalThrustDirection;
                    actual3Di = t3.LocalThrustDirection;
                    if (t2c < 0)
                    {
                        actual2Di = Base6Directions.GetOppositeDirection(t2.LocalThrustDirection);
                        t2c *= -1;
                    }
                    if (t3c < 0)
                    {
                        actual3Di = Base6Directions.GetOppositeDirection(t3.LocalThrustDirection);
                        t3c *= -1;
                    }
                    //// Publish the results
                    t1.finalThrustForces.X = t1.MaxThrust * maxPercentageThrustToUse;
                    t1.finalThrustForces.Y = t2c;
                    t1.finalThrustForces.Z = t3c;
                    t1.lambdaResult = Lambda;

                    t1.finalThrusterGroups[0] = t1;
                    t1.finalThrusterGroups[1] = thrusterGroups[actual2Di];
                    t1.finalThrusterGroups[2] = thrusterGroups[actual3Di];
                    t1.finalThrusterGroups[3] = thrusterGroups[Base6Directions.GetOppositeDirection(t1.LocalThrustDirection)];
                    t1.finalThrusterGroups[4] = thrusterGroups[Base6Directions.GetOppositeDirection(t2.LocalThrustDirection)];
                    t1.finalThrusterGroups[5] = thrusterGroups[Base6Directions.GetOppositeDirection(t3.LocalThrustDirection)];
                    //parent_program.Echo("Completed " + entry.Key.ToString());
                }

                ThrusterGroup bestCandidate = null;
                double bestCandidateLambda = -9999999;
                foreach (KeyValuePair<Base6Directions.Direction, ThrusterGroup> entry in thrusterGroups)
                {
                    if (entry.Value.lambdaResult > bestCandidateLambda)
                    {
                        if (entry.Value.finalThrustForces.Y <= entry.Value.finalThrusterGroups[1].MaxThrust + 1 &&
                            entry.Value.finalThrustForces.Z <= entry.Value.finalThrusterGroups[2].MaxThrust + 1)
                        {
                            bestCandidate = entry.Value;
                            bestCandidateLambda = entry.Value.lambdaResult;
                        }
                    }
                }
                //parent_program.Echo("Finished");
                //parent_program.Echo("Best: " + bestCandidate.LocalThrustDirection.ToString());
                return bestCandidate;

            }

            public ThrusterGroup SolvePartialThrust(Vector3D g, Vector3D targetThrust)
            {
                Vector3D resultantForce = -g + targetThrust;
                ThrusterGroup t1 = FindThrusterGroupsInDirection(resultantForce);
                //var mat = new double[,] {
                //    { t1.finalThrusterGroups[0].WorldThrustDirection.X, t1.finalThrusterGroups[1].WorldThrustDirection.X, t1.finalThrusterGroups[2].WorldThrustDirection.X },
                //    { t1.finalThrusterGroups[0].WorldThrustDirection.Y, t1.finalThrusterGroups[1].WorldThrustDirection.Y, t1.finalThrusterGroups[2].WorldThrustDirection.Y },
                //    { t1.finalThrusterGroups[0].WorldThrustDirection.Z, t1.finalThrusterGroups[1].WorldThrustDirection.Z, t1.finalThrusterGroups[2].WorldThrustDirection.Z },
                //};
                t1.matrixM[0, 0] = t1.finalThrusterGroups[0].WorldThrustDirection.X;
                t1.matrixM[0, 1] = t1.finalThrusterGroups[1].WorldThrustDirection.X;
                t1.matrixM[0, 2] = t1.finalThrusterGroups[2].WorldThrustDirection.X;

                t1.matrixM[1, 0] = t1.finalThrusterGroups[0].WorldThrustDirection.Y;
                t1.matrixM[1, 1] = t1.finalThrusterGroups[1].WorldThrustDirection.Y;
                t1.matrixM[1, 2] = t1.finalThrusterGroups[2].WorldThrustDirection.Y;

                t1.matrixM[2, 0] = t1.finalThrusterGroups[0].WorldThrustDirection.Z;
                t1.matrixM[2, 1] = t1.finalThrusterGroups[1].WorldThrustDirection.Z;
                t1.matrixM[2, 2] = t1.finalThrusterGroups[2].WorldThrustDirection.Z;


                t1.ANS[0] = resultantForce.X;
                t1.ANS[1] = resultantForce.Y;
                t1.ANS[2] = resultantForce.Z;

                //t1.ANS WorldDirectionForce.X, WorldDirectionForce.Y, WorldDirectionForce.Z };
                PID.ComputeCoefficients(t1.matrixM, t1.ANS);
                t1.finalThrustForces.X = t1.ANS[0];
                t1.finalThrustForces.Y = t1.ANS[1];
                t1.finalThrustForces.Z = t1.ANS[2];

                return t1;
            }

            public ThrusterGroup FindThrusterGroupsInDirection(Vector3D _WorldDirection)
            {
                List<ThrusterGroup> AccumulatorGroup = new List<ThrusterGroup>();
                List<ThrusterGroup> OtherGroup = new List<ThrusterGroup>();
                Vector3D WorldDirection = Vector3D.Normalize(_WorldDirection);
                foreach (KeyValuePair<Base6Directions.Direction, ThrusterGroup> entry in thrusterGroups)
                {
                    ThrusterGroup currentThrusterGroup = entry.Value;
                    if (currentThrusterGroup.WorldThrustDirection.Dot(WorldDirection) > 0)
                    {
                        AccumulatorGroup.Add(currentThrusterGroup);
                    }
                    else
                    {
                        OtherGroup.Add(currentThrusterGroup);
                    }
                }
                if (AccumulatorGroup.Count > 3)
                {
                    AccumulatorGroup.Sort(delegate (ThrusterGroup c1, ThrusterGroup c2) {
                        if (c1.WorldThrustDirection.Dot(WorldDirection) > c2.WorldThrustDirection.Dot(WorldDirection))
                        {
                            return -1;
                        }
                        else
                        {
                            return 1;
                        }
                    });
                    for (int i = 3; i < AccumulatorGroup.Count; i++)
                    {
                        OtherGroup.Add(AccumulatorGroup[i]);
                    }
                    parent_program.shipIOHandler.Echo("Warning, more than 3 viable thruster groups found! removing extra ones");
                }
                if (AccumulatorGroup.Count < 3)
                {
                    parent_program.shipIOHandler.Error("Only two viable thruster groups found!\nPlease rotate the ship slightly and recompile, that could fix this.");
                }
                ThrusterGroup t1 = AccumulatorGroup[0];
                t1.finalThrusterGroups[0] = AccumulatorGroup[0];
                t1.finalThrusterGroups[1] = AccumulatorGroup[1];
                t1.finalThrusterGroups[2] = AccumulatorGroup[2];
                // BELOW ARE UNUSED GROUPS!
                t1.finalThrusterGroups[3] = OtherGroup[0];
                t1.finalThrusterGroups[4] = OtherGroup[1];
                t1.finalThrusterGroups[5] = OtherGroup[2];

                return t1;
            }



            #region StartupCalculations

            /// <summary>Gathers the blocks that relate to the ship. This includes:<br />The cockpit, thrusters, gyros, (main) connector.</summary>
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
                

                //myConnector = FindConnector();
                thrusters = FindThrusters();
                gyros = FindGyros();
                if (!parent_program.errorState)
                {
                    if (firstTime)
                    {
                        parent_program.shipIOHandler.Echo("Waiting for orders, Your Highness.\n");
                        if (parent_program.extra_info) { 
                        if(shipMass != 0)
                            {
                                parent_program.shipIOHandler.Echo("Mass: " + shipMass.ToString());
                            }
                        parent_program.shipIOHandler.Echo("Thruster count: " + thrusters.Count.ToString());
                        parent_program.shipIOHandler.Echo("Gyro count: " + gyros.Count.ToString());
                        parent_program.shipIOHandler.Echo("Main control: " + cockpit.CustomName);
                            //if (parent_program.homeLocations.Count > 0) {
                        parent_program.shipIOHandler.OutputHomeLocations();
                        //}
                        }
                        
                        parent_program.shipIOHandler.EchoFinish(false);
                    }
                }

                Populate6ThrusterGroups();

                firstTime = false;
            }

            /// <summary>Checks if a given block is on the same grid as the programming block</summary><param name="block"></param><returns>bool</returns>
            public bool blockIsOnMyGrid(IMyTerminalBlock block)
            {
                return block.CubeGrid.EntityId == parent_program.Me.CubeGrid.EntityId;
            }

            public static double GetRadiusOfConnector(IMyShipConnector con)
            {
                //var blockDimensions = Vector3I.Transform(block.Max - block.Min + Vector3I.One, MatrixD.Transpose(block.Orientation.GetMatrix()));
                //var point = block.GetPosition() + block.WorldMatrix.Forward * block.CubeGrid.GridSize * blockDimensions.Z * 0.5


                if (con.CubeGrid.GridSize == 0.5)
                {
                    return con.CubeGrid.GridSize;
                }
                else
                {
                    return con.CubeGrid.GridSize * 0.5;
                }
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

            /// <summary>When called, the ship will look through it's grid (And in-turn, it's connected grid) to find what connector the ship is connected to.<br />More specifically, it looks for a connector that is part of the cockpit's grid and is "connected".</summary><returns>IMyShipConnector</returns>
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

            public void Populate6ThrusterGroups()
            {
                thrusterGroups = new Dictionary<Base6Directions.Direction, ThrusterGroup>();

                ForwardThrust = new ThrusterGroup(parent_program, Base6Directions.Direction.Forward, cockpit);
                UpThrust = new ThrusterGroup(parent_program, Base6Directions.Direction.Up, cockpit);
                LeftThrust = new ThrusterGroup(parent_program, Base6Directions.Direction.Left, cockpit);
                BackwardThrust = new ThrusterGroup(parent_program, Base6Directions.Direction.Backward, cockpit);
                DownThrust = new ThrusterGroup(parent_program, Base6Directions.Direction.Down, cockpit);
                RightThrust = new ThrusterGroup(parent_program, Base6Directions.Direction.Right, cockpit);
                thrusterGroups.Add(Base6Directions.Direction.Forward, ForwardThrust);
                thrusterGroups.Add(Base6Directions.Direction.Up, UpThrust);
                thrusterGroups.Add(Base6Directions.Direction.Left, LeftThrust);
                thrusterGroups.Add(Base6Directions.Direction.Backward, BackwardThrust);
                thrusterGroups.Add(Base6Directions.Direction.Down, DownThrust);
                thrusterGroups.Add(Base6Directions.Direction.Right, RightThrust);
                Vector3D thrusterDirection;
                double forwardDot = 0;
                double upDot = 0;
                double leftDot = 0;
                foreach (IMyThrust thisThruster in thrusters)
                {
                    if (thisThruster.IsWorking && blockIsOnMyGrid(thisThruster))
                    {
                        thrusterDirection = -thisThruster.WorldMatrix.Forward;
                        forwardDot = Vector3D.Dot(thrusterDirection, cockpit.WorldMatrix.Forward);
                        upDot = Vector3D.Dot(thrusterDirection, cockpit.WorldMatrix.Up);
                        leftDot = Vector3D.Dot(thrusterDirection, cockpit.WorldMatrix.Left);

                        if (forwardDot >= 0.97)
                            thrusterGroups[Base6Directions.Direction.Forward].AddThruster(thisThruster);
                        else if (leftDot >= 0.97)
                            thrusterGroups[Base6Directions.Direction.Left].AddThruster(thisThruster);
                        else if (upDot >= 0.97)
                            thrusterGroups[Base6Directions.Direction.Up].AddThruster(thisThruster);
                        else if (forwardDot <= -0.97)
                            thrusterGroups[Base6Directions.Direction.Backward].AddThruster(thisThruster);
                        else if (leftDot <= -0.97)
                            thrusterGroups[Base6Directions.Direction.Right].AddThruster(thisThruster);
                        else if (upDot <= -0.97)
                            thrusterGroups[Base6Directions.Direction.Down].AddThruster(thisThruster);
                    }


                }
            }

            public void UpdateThrusterGroupsWorldDirections()
            {
                foreach (KeyValuePair<Base6Directions.Direction, ThrusterGroup> entry in thrusterGroups)
                {
                    entry.Value.UpdateWorldDirection();
                }
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

            #region Debug

            public double CheckAccelerationFromThrusters(ThrusterGroup thrusterGroupToUse, Vector3D gravity_and_unknown)
            {
                Vector3D totalThrusterForce = Vector3D.Zero;
                
                totalThrusterForce += Vector3D.Normalize(thrusterGroupToUse.finalThrusterGroups[0].WorldThrustDirection) * thrusterGroupToUse.finalThrustForces.X;
                totalThrusterForce += Vector3D.Normalize(thrusterGroupToUse.finalThrusterGroups[1].WorldThrustDirection) * thrusterGroupToUse.finalThrustForces.Y;
                totalThrusterForce += Vector3D.Normalize(thrusterGroupToUse.finalThrusterGroups[2].WorldThrustDirection) * thrusterGroupToUse.finalThrustForces.Z;
                if (thrusterGroupToUse.finalThrustForces.X > thrusterGroupToUse.finalThrusterGroups[0].MaxThrust)
                {
                    Echo(thrusterGroupToUse.finalThrusterGroups[0].LocalThrustDirection.ToString() + " has not enough force: " + IOHandler.RoundToSignificantDigits(thrusterGroupToUse.finalThrustForces.X / thrusterGroupToUse.finalThrusterGroups[0].MaxThrust, 3).ToString());
                }
                if (thrusterGroupToUse.finalThrustForces.Y > thrusterGroupToUse.finalThrusterGroups[1].MaxThrust)
                {
                    Echo(thrusterGroupToUse.finalThrusterGroups[1].LocalThrustDirection.ToString() + " has not enough force: " + IOHandler.RoundToSignificantDigits(thrusterGroupToUse.finalThrustForces.Y / thrusterGroupToUse.finalThrusterGroups[1].MaxThrust, 3).ToString());
                }
                if (thrusterGroupToUse.finalThrustForces.Z > thrusterGroupToUse.finalThrusterGroups[2].MaxThrust)
                {
                    Echo(thrusterGroupToUse.finalThrusterGroups[2].LocalThrustDirection.ToString() + " has not enough force: " + IOHandler.RoundToSignificantDigits(thrusterGroupToUse.finalThrustForces.Z / thrusterGroupToUse.finalThrusterGroups[2].MaxThrust, 3).ToString());
                }

                Vector3D resultantNoGravT = totalThrusterForce + gravity_and_unknown;

                return resultantNoGravT.Length() / shipMass;
            }

            #endregion

        }

        public class ThrusterGroup
        {

            private readonly Program parent_program;
            public IMyTerminalBlock directionReferenceBlock;
            public Base6Directions.Direction LocalThrustDirection;
            public List<IMyThrust> thrusters;
            public Vector3D WorldThrustDirection;
            public int directionSign = 1;

            // Temporary variables, defined here so they aren't defined every frame.
            public double lambdaResult = 0;
            public double[,] matrixM;
            public double[] ANS;
            public Vector3D finalThrustForces;
            public ThrusterGroup[] finalThrusterGroups;


            /// <summary>Force In Newtons</summary>
            public double MaxThrust;
            
            public ThrusterGroup(Program _parent_program, Base6Directions.Direction direction, IMyTerminalBlock _directionReferenceBlock)
            {
                parent_program = _parent_program;
                directionReferenceBlock = _directionReferenceBlock;
                thrusters = new List<IMyThrust>();
                MaxThrust = 0;
                LocalThrustDirection = direction;
                UpdateWorldDirection();
                ANS = new double[3];
                finalThrustForces = new Vector3D();
                finalThrusterGroups = new ThrusterGroup[6];
                matrixM = new double[3,3];

                if (LocalThrustDirection == Base6Directions.Direction.Down ||
                    LocalThrustDirection == Base6Directions.Direction.Right ||
                    LocalThrustDirection == Base6Directions.Direction.Backward)
                {
                    directionSign = -1;
                }
                else
                {
                    directionSign = 1;
                }
            }

            public void AddThruster(IMyThrust thruster)
            {
                thrusters.Add(thruster);
                MaxThrust += thruster.MaxEffectiveThrust;
                thruster.CustomName = LocalThrustDirection.ToString() + " " + thrusters.Count.ToString();
            }

            public void UpdateWorldDirection()
            {
                WorldThrustDirection = directionReferenceBlock.WorldMatrix.GetDirectionVector(LocalThrustDirection);
            }

        }

        public class ThrusterGroupResult
        {
            public Vector3D finalThrustForces;
            public ThrusterGroup[] finalThrusterGroups;
            public double lambdaResult;

            public ThrusterGroupResult(ThrusterGroup group_to_copy)
            {
                finalThrustForces.X = group_to_copy.finalThrustForces.X;
                finalThrustForces.Y = group_to_copy.finalThrustForces.Y;
                finalThrustForces.Z = group_to_copy.finalThrustForces.Z;

                finalThrusterGroups = new ThrusterGroup[6];
                finalThrusterGroups[0] = group_to_copy.finalThrusterGroups[0];
                finalThrusterGroups[1] = group_to_copy.finalThrusterGroups[1];
                finalThrusterGroups[2] = group_to_copy.finalThrusterGroups[2];
                finalThrusterGroups[3] = group_to_copy.finalThrusterGroups[3];
                finalThrusterGroups[4] = group_to_copy.finalThrusterGroups[4];
                finalThrusterGroups[5] = group_to_copy.finalThrusterGroups[5];
                finalThrusterGroups[6] = group_to_copy.finalThrusterGroups[6];
                lambdaResult = group_to_copy.lambdaResult;
            }
        }
    }
}
