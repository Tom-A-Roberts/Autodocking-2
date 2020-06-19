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
        /// <summary>This class is used as a modularized section of code (for cleanliness) that handles all analysis of the ship's systems.<br />This includes finding the ship mass, main cockpit, connectors, thrusters, gyros etc.</summary>
        public class ShipSystemsAnalyzer
        {
            public Program parent_program;
            private bool firstTime = true;

            /// <summary>The main Cockpit of the ship, used to define mass, "Forwards" and other coordinate systems.</summary>
            public IMyShipController cockpit;

            public IMyShipConnector myConnector; // OUTDATED MUST CHANGE!

            public List<IMyThrust> thrusters = new List<IMyThrust>();
            public List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            public List<IMyGyro> gyros = new List<IMyGyro>();

            /// <summary>Mass of the ship in Kilograms</summary>
            public float shipMass = 9999;
            /// <summary>Mass of the ship in Kilograms, the last time the script was run. This is used to detect changes in the mass.</summary>
            public float previousShipMass = 9999;

            /// <summary>Directions are with respect to the cockpit.</summary>
            public ThrusterGroup ForwardThrust;
            public ThrusterGroup UpThrust;
            public ThrusterGroup LeftThrust;
            public ThrusterGroup BackwardThrust;
            public ThrusterGroup DownThrust;
            public ThrusterGroup RightThrust;
            public Dictionary<Base6Directions.Direction,ThrusterGroup> thrusterGroups;

            // temporary calculation variables:

            public ShipSystemsAnalyzer(Program in_parent_program)
            {
                parent_program = in_parent_program;
                parent_program.shipIOHandler.Echo("INITIALIZED\n");
                GatherBasicData();
            }




            public double[] CalculateThrusterGroupsPower(Vector3D WorldDirectionForce, ThrusterGroup[] relevantThrustGroups)
            {
                var mat = new double[,] {
                    { relevantThrustGroups[0].WorldThrustDirection.X, relevantThrustGroups[1].WorldThrustDirection.X, relevantThrustGroups[2].WorldThrustDirection.X },
                    { relevantThrustGroups[0].WorldThrustDirection.Y, relevantThrustGroups[1].WorldThrustDirection.Y, relevantThrustGroups[2].WorldThrustDirection.Y },
                    { relevantThrustGroups[0].WorldThrustDirection.Z, relevantThrustGroups[1].WorldThrustDirection.Z, relevantThrustGroups[2].WorldThrustDirection.Z },
                };

                var ans = new double[] { WorldDirectionForce.X, WorldDirectionForce.Y, WorldDirectionForce.Z };
                PID.ComputeCoefficients(mat, ans);
                if (ans[0] > relevantThrustGroups[0].MaxThrust)
                {
                    ans[0] = relevantThrustGroups[0].MaxThrust;
                }
                if (ans[1] > relevantThrustGroups[1].MaxThrust)
                {
                    ans[1] = relevantThrustGroups[1].MaxThrust;
                }
                if (ans[2] > relevantThrustGroups[2].MaxThrust)
                {
                    ans[2] = relevantThrustGroups[2].MaxThrust;
                }
                return ans;
            }

            
            public void CalculateThrusterGroupsPower2(Vector3D WorldDirectionForce, ThrusterGroup[] relevantThrustGroups)
            {
                //var mat = new double[,] {
                //    { relevantThrustGroups[0].WorldThrustDirection.X, relevantThrustGroups[1].WorldThrustDirection.X, relevantThrustGroups[2].WorldThrustDirection.X },
                //    { relevantThrustGroups[0].WorldThrustDirection.Y, relevantThrustGroups[1].WorldThrustDirection.Y, relevantThrustGroups[2].WorldThrustDirection.Y },
                //    { relevantThrustGroups[0].WorldThrustDirection.Z, relevantThrustGroups[1].WorldThrustDirection.Z, relevantThrustGroups[2].WorldThrustDirection.Z },
                //};
                Matrix matrixM = Matrix.Identity;
                matrixM.SetRow(0, new Vector4((float)relevantThrustGroups[0].WorldThrustDirection.X, (float)relevantThrustGroups[0].WorldThrustDirection.Y, (float)relevantThrustGroups[0].WorldThrustDirection.Z, 0));
                matrixM.SetRow(1, new Vector4((float)relevantThrustGroups[1].WorldThrustDirection.X, (float)relevantThrustGroups[1].WorldThrustDirection.Y, (float)relevantThrustGroups[1].WorldThrustDirection.Z, 0));
                matrixM.SetRow(2, new Vector4((float)relevantThrustGroups[2].WorldThrustDirection.X, (float)relevantThrustGroups[2].WorldThrustDirection.Y, (float)relevantThrustGroups[2].WorldThrustDirection.Z, 0));
                matrixM.SetRow(3, new Vector4(0, 0, 0, 1));
                Vector3D equalityValues = new Vector3D(WorldDirectionForce.X, WorldDirectionForce.Y, WorldDirectionForce.Z);
                //Vector3D returnvalues = new Vector3D();

                //SolveThrusterSimultaneousEquations(matrixM, equalityValues, Base6Directions.Direction.Forward);


                //returnValues = Vector3D.TransformNormal(equalityValues, Matrix.Invert(matrixM));

            }




            public ThrusterGroup SolveMaxThrust(Vector3D g, Vector3D targetDirection)
            {
                Base6Directions.Direction actual2Di;// = t2.LocalThrustDirection;
                Base6Directions.Direction actual3Di;// = t3.LocalThrustDirection;


                foreach (KeyValuePair<Base6Directions.Direction, ThrusterGroup> entry in thrusterGroups)
                {
                    ThrusterGroup t1 = entry.Value;
                    ThrusterGroup t2;
                    ThrusterGroup t3;

                    //Base6Directions.Direction axis1 = Base6Directions.Direction.Up;
                    //Base6Directions.Direction axis2 = Base6Directions.Direction.Up;
                    //Base6Directions.Direction axis3 = Base6Directions.Direction.Up;


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

                    t1.matrixM.SetRow(0, new Vector4D(t2.WorldThrustDirection.X, t2.WorldThrustDirection.Y, t2.WorldThrustDirection.Z, 0));
                    t1.matrixM.SetRow(1, new Vector4D(t3.WorldThrustDirection.X, t3.WorldThrustDirection.Y, t3.WorldThrustDirection.Z, 0));
                    t1.matrixM.SetRow(2, new Vector4D(-targetDirection.X, -targetDirection.Y, -targetDirection.Z, 0));
                    t1.matrixM.SetRow(3, new Vector4D(0,0,0,1));

                    t1.equalityValues.X = (-t1.directionSign * t1.MaxThrust * t1.WorldThrustDirection.X) + g.X;
                    t1.equalityValues.Y = (-t1.directionSign * t1.MaxThrust * t1.WorldThrustDirection.Y) + g.Y;
                    t1.equalityValues.Z = (-t1.directionSign * t1.MaxThrust * t1.WorldThrustDirection.Z) + g.Z;

                    t1.matrixResult = Vector3D.TransformNormal(t1.equalityValues, Matrix.Invert(t1.matrixM));

                    //double t2c = t1.matrixResult.X;
                    //double t3c = t1.matrixResult.Y;
                    //double Lambda =
                   

                    actual2Di = t2.LocalThrustDirection;
                    actual3Di = t3.LocalThrustDirection;
                    if (t1.matrixResult.X < 0)
                    {
                        actual2Di = Base6Directions.GetOppositeDirection(t2.LocalThrustDirection);
                        t1.matrixResult.X *= -1;
                    }
                    if (t1.matrixResult.Y < 0)
                    {
                        actual3Di = Base6Directions.GetOppositeDirection(t3.LocalThrustDirection);
                        t1.matrixResult.Y *= -1;
                    }
                    // Publish the results
                    t1.finalThrustForces.X = t1.MaxThrust;
                    t1.finalThrustForces.Y = t1.matrixResult.X;
                    t1.finalThrustForces.Z = t1.matrixResult.Y;
                    t1.lambdaResult = t1.matrixResult.Z;

                    t1.finalThrusterGroups[0] = t1;
                    t1.finalThrusterGroups[1] = thrusterGroups[actual2Di];
                    t1.finalThrusterGroups[2] = thrusterGroups[actual3Di];

                    t1.finalThrusterGroups[3] = thrusterGroups[Base6Directions.GetOppositeDirection(t1.LocalThrustDirection)];
                    t1.finalThrusterGroups[4] = thrusterGroups[Base6Directions.GetOppositeDirection(actual2Di)];
                    t1.finalThrusterGroups[5] = thrusterGroups[Base6Directions.GetOppositeDirection(actual3Di)];

                    #region Old comments

                    // If "Forward" is chosen
                    //if (t1.LocalThrustDirection == Base6Directions.Direction.Forward)
                    //{


                    //parent_program.shipIOHandler.Echo(t1.LocalThrustDirection.ToString() + ": " + (t1.MaxThrust / t1.MaxThrust).ToString());
                    //parent_program.shipIOHandler.Echo(actual2Di.ToString() + ": " + (t2c / thrusterGroups[actual2Di].MaxThrust).ToString());
                    //parent_program.shipIOHandler.Echo(actual3Di.ToString() + ": " + (t3c / thrusterGroups[actual3Di].MaxThrust).ToString());
                    //parent_program.shipIOHandler.Echo("Lambda: " + Lambda.ToString());

                    //if (actual2Di == Base6Directions.Direction.Down)
                    //{
                    //    parent_program.shipIOHandler.Echo("Down thruster name: " + thrusterGroups[actual2Di].thrusters[0].DisplayNameText);
                    //}
                    ////if (actualt2Di == Base6Directions.Direction.Right)
                    ////{
                    ////    parent_program.shipIOHandler.Echo("Right thruster name: " + thrusterGroups[actualt2Di].thrusters[0].DisplayNameText);
                    ////}

                    //if (actual3Di == Base6Directions.Direction.Down)
                    //{
                    //    parent_program.shipIOHandler.Echo("Down thruster name: " + thrusterGroups[actual3Di].thrusters[0].DisplayNameText);
                    //}
                    ////if (actualt3Di == Base6Directions.Direction.Right)
                    ////{
                    ////    parent_program.shipIOHandler.Echo("Right thruster name: " + thrusterGroups[actualt3Di].thrusters[0].DisplayNameText);
                    ////}
                    //}
                    #endregion

                }

                ThrusterGroup bestCandidate = null;
                double bestCandidateLambda = 0;

                string ostr = "";
                foreach (KeyValuePair<Base6Directions.Direction, ThrusterGroup> entry in thrusterGroups)
                {
                    if(entry.Value.lambdaResult > bestCandidateLambda)
                    {
                        if(entry.Value.finalThrustForces.Y <= entry.Value.finalThrusterGroups[1].MaxThrust + 1 &&
                            entry.Value.finalThrustForces.Z <= entry.Value.finalThrusterGroups[2].MaxThrust + 1)
                        {
                            bestCandidate = entry.Value;
                            bestCandidateLambda = entry.Value.lambdaResult;
                        }
                    }
                    ostr += entry.Value.LocalThrustDirection.ToString() + " Lambda: " + entry.Value.lambdaResult.ToString() + "\n";
                    ostr += entry.Value.finalThrusterGroups[1].LocalThrustDirection.ToString() + " C2: " + (entry.Value.finalThrustForces.Y / entry.Value.finalThrusterGroups[1].MaxThrust).ToString() + "\n";
                    ostr += entry.Value.finalThrusterGroups[2].LocalThrustDirection.ToString() + " C3: " + (entry.Value.finalThrustForces.Z / entry.Value.finalThrusterGroups[2].MaxThrust).ToString() + "\n";
                }


                //if (bestCandidate == null)
                //{
                //    parent_program.shipIOHandler.Error(ostr);
                //}
                parent_program.shipIOHandler.Echo(ostr);

                return bestCandidate;

            }



            public void SolveThrusterSimultaneousEquations(Vector3D equalityValues, Base6Directions.Direction thrusterGroup)
            {


                thrusterGroups[thrusterGroup].matrixResult = Vector3D.TransformNormal(equalityValues, Matrix.Invert(thrusterGroups[thrusterGroup].matrixM));
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

                Populate6ThrusterGroups();

                firstTime = false;
            }

            /// <summary>Checks if a given block is on the same grid as the programming block</summary><param name="block"></param><returns>bool</returns>
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

                ForwardThrust = new ThrusterGroup(parent_program, Base6Directions.Direction.Forward);
                UpThrust = new ThrusterGroup(parent_program, Base6Directions.Direction.Up);
                LeftThrust = new ThrusterGroup(parent_program, Base6Directions.Direction.Left);
                BackwardThrust = new ThrusterGroup(parent_program, Base6Directions.Direction.Backward);
                DownThrust = new ThrusterGroup(parent_program, Base6Directions.Direction.Down);
                RightThrust = new ThrusterGroup(parent_program, Base6Directions.Direction.Right);
                thrusterGroups.Add(Base6Directions.Direction.Forward, ForwardThrust);
                thrusterGroups.Add(Base6Directions.Direction.Up, UpThrust);
                thrusterGroups.Add(Base6Directions.Direction.Left, LeftThrust);
                thrusterGroups.Add(Base6Directions.Direction.Backward, BackwardThrust);
                thrusterGroups.Add(Base6Directions.Direction.Down, DownThrust);
                thrusterGroups.Add(Base6Directions.Direction.Right, RightThrust);
                foreach (IMyThrust thisThruster in thrusters)
                {
                    if (blockIsOnMyGrid(thisThruster))
                    {
                        Base6Directions.Direction thrusterLocalDirection = thisThruster.Orientation.Forward;
                        ThrusterGroup relevantThrusterGroup = thrusterGroups[thrusterLocalDirection];
                        relevantThrusterGroup.AddThruster(thisThruster);
                    }
                    #region WhatToDoIfThrusterIsNotOnSameGrid

                    //else
                    //{
                    //    var thrusterDirection = -thisThruster.WorldMatrix.Forward;
                    //    double forwardDot = Vector3D.Dot(thrusterDirection, Vector3D.Normalize(thrustForward));
                    //    double leftDot = Vector3D.Dot(thrusterDirection, Vector3D.Normalize(thrustLeft));
                    //    double upDot = Vector3D.Dot(thrusterDirection, Vector3D.Normalize(thrustUp));

                    //    if (forwardDot >= 0.97)
                    //    {
                    //        ForceForwardThrusters.Add(thisThruster);
                    //    }
                    //    else if (leftDot >= 0.97)
                    //    {
                    //        ForceLeftThrusters.Add(thisThruster);
                    //    }
                    //    else if (upDot >= 0.97)
                    //    {
                    //        ForceUpThrusters.Add(thisThruster);
                    //    }
                    //    else
                    //    {
                    //        UnusedThrusters.Add(thisThruster);
                    //    }
                    //}

                    #endregion

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

            // Old, do not open....
            #region Deprecated
            public ThrusterGroup[] FindThrusterGroupsInDirection(Vector3D _WorldDirection)
            {
                ThrusterGroup[] OutputGroup = new ThrusterGroup[6];
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
                OutputGroup[0] = AccumulatorGroup[0];
                OutputGroup[1] = AccumulatorGroup[1];
                OutputGroup[2] = AccumulatorGroup[2];
                // BELOW ARE UNUSED GROUPS!
                OutputGroup[3] = OtherGroup[0];
                OutputGroup[4] = OtherGroup[1];
                OutputGroup[5] = OtherGroup[2];

                return OutputGroup;
            }


            /// <summary>
            /// Finds the thrust (Newtons) available in the ship after gravity is accounted for. <br />
            /// The leftover thrusts must be up to-date: PopulateThrusterGroupsLeftoverThrust()
            /// </summary>
            /// <param name="thrust_direction"></param>
            /// <returns></returns>
            public double FindMaxAvailableThrustInDirection(Vector3D thrust_direction)
            {
                ThrusterGroup[] relevantThrusterGroups = FindThrusterGroupsInDirection(thrust_direction);
                double a = relevantThrusterGroups[0].LeftoverThrust;
                double b = relevantThrusterGroups[1].LeftoverThrust;
                double c = relevantThrusterGroups[2].LeftoverThrust;

                double a1 = thrust_direction.X;
                double a2 = thrust_direction.Y;
                double a3 = thrust_direction.Z;

                double Alpha = a1 / a;
                double Beta = a2 / b;
                double Gamma = a3 / c;

                double max = Math.Max(Math.Max(Alpha, Beta), Gamma);
                double t = 1 / max;
                return (t * Vector3D.Normalize(thrust_direction)).Length();
            }

            public void PopulateThrusterGroupsLeftoverThrust(Vector3D Gravity_And_Unknown_Force)
            {


                ThrusterGroup[] gravityThrusterGroupsToUse = FindThrusterGroupsInDirection(Gravity_And_Unknown_Force);

                double[] thrustsNeededForGravityAndUnknown = CalculateThrusterGroupsPower(Gravity_And_Unknown_Force, gravityThrusterGroupsToUse);

                gravityThrusterGroupsToUse[0].LeftoverThrust = gravityThrusterGroupsToUse[0].MaxThrust - thrustsNeededForGravityAndUnknown[0];
                gravityThrusterGroupsToUse[1].LeftoverThrust = gravityThrusterGroupsToUse[1].MaxThrust - thrustsNeededForGravityAndUnknown[1];
                gravityThrusterGroupsToUse[2].LeftoverThrust = gravityThrusterGroupsToUse[2].MaxThrust - thrustsNeededForGravityAndUnknown[2];

                gravityThrusterGroupsToUse[3].LeftoverThrust = gravityThrusterGroupsToUse[3].MaxThrust;
                gravityThrusterGroupsToUse[4].LeftoverThrust = gravityThrusterGroupsToUse[4].MaxThrust;
                gravityThrusterGroupsToUse[5].LeftoverThrust = gravityThrusterGroupsToUse[5].MaxThrust;
            }

            public double FindAmountOfForceInDirection(Vector3D TotalForce, Vector3D componentDirection)
            {
                double componentDirectionLength = componentDirection.Length();
                Vector3D projectedVector = (TotalForce.Dot(componentDirection) / componentDirectionLength) * (componentDirection / componentDirection.Length());
                return projectedVector.Length();
            }
            public Vector3D FindActualForceFromThrusters(ThrusterGroup thrusterGroupToUse)
            {
                Vector3D totalThrusterForce = Vector3D.Zero;
                totalThrusterForce += Vector3D.Normalize(thrusterGroupToUse.finalThrusterGroups[0].WorldThrustDirection) * thrusterGroupToUse.finalThrustForces.X;
                totalThrusterForce += Vector3D.Normalize(thrusterGroupToUse.finalThrusterGroups[1].WorldThrustDirection) * thrusterGroupToUse.finalThrustForces.Y;
                totalThrusterForce += Vector3D.Normalize(thrusterGroupToUse.finalThrusterGroups[2].WorldThrustDirection) * thrusterGroupToUse.finalThrustForces.Z;
                return totalThrusterForce;
            }

            public struct ThrusterForceAnalysis
            {
                ShipSystemsAnalyzer systems_parent;

                public List<IMyThrust> ForceForwardThrusters; // Forward with respect to the direction the thrusters will be firing
                public List<IMyThrust> ForceLeftThrusters;
                public List<IMyThrust> ForceUpThrusters;
                public List<IMyThrust> UnusedThrusters;
                // Accumulated max thrust numbers
                public double ForwardMaxThrust;
                public double LeftMaxThrust;
                public double UpMaxThrust;

                /// <summary>
                /// This is the raw value (not including gravity and such) that the thrusters can possibly push the ship in the given direction.
                /// </summary>
                //public double maxAvailableThrustInDirection; 
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

                public double FindMaxAvailableThrustInDirection(double f_max_thrust, double u_max_thrust, double l_max_thrust, Vector3D thrust_direction)
                {
                    double a = f_max_thrust;
                    double b = u_max_thrust;
                    double c = l_max_thrust;

                    double a1 = thrust_direction.X;
                    double a2 = thrust_direction.Y;
                    double a3 = thrust_direction.Z;

                    double Alpha = a1 / a;
                    double Beta = a2 / b;
                    double Gamma = a3 / c;

                    double max = Math.Max(Math.Max(Alpha, Beta), Gamma);
                    double t = 1 / max;
                    return (t * Vector3D.Normalize(thrust_direction)).Length();
                }
            }

            #endregion

        }

        public class ThrusterGroup
        {
            private readonly Program parent_program;
            public Base6Directions.Direction LocalThrustDirection;
            public List<IMyThrust> thrusters;
            public Vector3D WorldThrustDirection;

            // Temporary variables, defined here so they aren't defined every frame.
            public Vector3D matrixResult;
            public double lambdaResult = 0;
            public Matrix matrixM;
            public Vector3D equalityValues;
            public Vector3D finalThrustForces;
            public ThrusterGroup[] finalThrusterGroups;

            public int directionSign = 1;

            /// <summary>Force In Newtons</summary>
            public double MaxThrust;
            /// <summary>Thrust leftover, after gravity + unknown forces are accounted for</summary>
            public double LeftoverThrust = 0;
            
            public ThrusterGroup(Program _parent_program, Base6Directions.Direction direction)
            {
                parent_program = _parent_program;
                thrusters = new List<IMyThrust>();
                MaxThrust = 0;
                LocalThrustDirection = direction;
                UpdateWorldDirection();
                matrixResult = new Vector3D();
                equalityValues = new Vector3D();
                finalThrustForces = new Vector3D();
                finalThrusterGroups = new ThrusterGroup[6];
                matrixM = new Matrix();
                matrixM = Matrix.Identity;
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
            }

            public void UpdateWorldDirection()
            {
                Vector3 referenceOrigin = parent_program.Me.CubeGrid.GetPosition();
                IMyCubeGrid gridWithRespectTo = parent_program.Me.CubeGrid;
                WorldThrustDirection = gridWithRespectTo.WorldMatrix.GetDirectionVector(LocalThrustDirection);
            }

        }
    
        

    }
}
