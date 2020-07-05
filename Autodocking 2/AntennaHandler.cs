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
        public class AntennaHandler
        {
            public Program parent_program;
            const string _responseTag = "Spug's position update response";
            const string _outgoingRequestTag = "Spug's position update request";
            const string _recallRequestTag = "Spug's recall request";
            public IMyRadioAntenna antenna = null;
            IMyUnicastListener _myUnicastListener;
            IMyBroadcastListener _myBroadcastListener;
            public List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();

            public AntennaHandler(Program _program)
            {
                parent_program = _program;

                _myBroadcastListener = parent_program.IGC.RegisterBroadcastListener(_recallRequestTag);
                _myUnicastListener = parent_program.IGC.UnicastListener;
                //_myUnicastListener = parent_program.IGC.RegisterBroadcastListener(_responseTag);
                _myUnicastListener.SetMessageCallback("UNICAST");
                _myBroadcastListener.SetMessageCallback(_recallRequestTag);
            }

            public string CheckAntenna()
            {
                
                int antenna_found_success = 0;

                parent_program.GridTerminalSystem.GetBlocks(blocks);
                
                foreach (var block in blocks)
                {
                    if (block is IMyRadioAntenna && blockIsOnMyGrid(block))
                    {
                        
                        IMyRadioAntenna my_antenna = (IMyRadioAntenna)block;
                        if (antenna_found_success < 1)
                        {
                            antenna_found_success = 1;
                            antenna = my_antenna;
                        }
                        if (my_antenna.Enabled && antenna_found_success < 2)
                        {
                            antenna_found_success = 2;
                            antenna = my_antenna;
                        }
                        if (my_antenna.EnableBroadcasting && my_antenna.Enabled && antenna_found_success < 3)
                        {
                            antenna_found_success = 3;
                            antenna = my_antenna;
                        }
                    }
                }
                if(antenna_found_success == 3)
                {
                    return "Antenna found:\nReady for use with the\noptional home script.";
                }
                else
                {
                    return "";
                }
            }
        
            public void SendPositionUpdateRequest(long target_platform)
            {
                long target_connector_id = parent_program.systemsAnalyzer.currentHomeLocation.stationConnectorID;
                long target_grid_id = parent_program.systemsAnalyzer.currentHomeLocation.stationGridID;
                parent_program.IGC.SendBroadcastMessage(_outgoingRequestTag, target_connector_id.ToString() + ";" + target_grid_id.ToString());
            }
            public bool blockIsOnMyGrid(IMyTerminalBlock block)
            {
                return block.CubeGrid.EntityId == parent_program.Me.CubeGrid.EntityId;
            }

            public void HandleMessage()
            {
                //handle broadcasts:
                while (_myBroadcastListener.HasPendingMessage)
                {
                    MyIGCMessage myIGCMessage = _myBroadcastListener.AcceptMessage();
                    if (myIGCMessage.Tag == _recallRequestTag)
                    {
                        string arg = myIGCMessage.Data.ToString();
                        parent_program.shipIOHandler.Echo("Broadcast received. This ship has been ordered to dock.");

                        HomeLocation arg_test = parent_program.FindHomeLocation(arg);
                        if(arg_test != null)
                        {
                            parent_program.Main(arg, UpdateType.Script);
                        }
                        
                    }
                }


                // handle unicasts:
                bool bFoundMessages = false;
                do
                {
                    bFoundMessages = false;
                    if (_myUnicastListener.HasPendingMessage)
                    {
                        bFoundMessages = true;
                        var msg = _myUnicastListener.AcceptMessage();
                        if (msg.Tag == _responseTag)
                        {
                            ParsePositionalResponse(msg.Data.ToString());
                            //parent_program.shipIOHandler.Echo("Unicast received. Data: " + .ToString());
                        //parent_program.shipIOHandler.Echo("Tag: " + msg.Tag.ToString());
                        }
                    }
                } while (bFoundMessages);
            }
            private void ParsePositionalResponse(string data)
            {
                //parent_program.shipIOHandler.Echo("Raw data:\n" + data);

                string[] data_parts = data.Split(';');

                if(data_parts.Length == 1)
                {
                    //Therefore an error has occurred.
                    //Print the error.
                    parent_program.shipIOHandler.Clear();
                    parent_program.shipIOHandler.Echo(data);
                    parent_program.SafelyExit();
                    parent_program.shipIOHandler.EchoFinish();

                }
                else if(data_parts.Length > 1)
                {
                    parent_program.hasConnectionToAntenna = true;

                    //messages_recieved += 1;
                    //parent_program.runningIssues = "Recieved: " + messages_recieved.ToString();
                    string result = parent_program.systemsAnalyzer.currentHomeLocation.UpdateDataFromOptionalHomeScript(data_parts);

                    if(result.Length > 0)
                    {
                        parent_program.runningIssues = result;
                    }
                }

            }

        }
    }
}
