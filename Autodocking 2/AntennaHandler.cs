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
            public IMyRadioAntenna antenna = null;
            IMyUnicastListener _myUnicastListener;
            public List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();

            public AntennaHandler(Program _program)
            {
                parent_program = _program;
               
                _myUnicastListener = parent_program.IGC.UnicastListener;
                _myUnicastListener.SetMessageCallback("UNICAST");

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
                    return "Antenna found. The script is ready to be used with an optional home script.";
                }
                else
                {
                    return "";
                }
            }
        
            public void SendPositionUpdateRequest(long target_platform)
            {
                long target_connector_id = parent_program.systemsAnalyzer.currentHomeLocation.stationConnectorID;
                parent_program.IGC.SendBroadcastMessage(_outgoingRequestTag, target_connector_id);
            }
            public bool blockIsOnMyGrid(IMyTerminalBlock block)
            {
                return block.CubeGrid.EntityId == parent_program.Me.CubeGrid.EntityId;
            }

            public void HandleMessage()
            {
                bool bFoundMessages = false;
                parent_program.shipIOHandler.Echo("HERE");

                do
                {
                    bFoundMessages = false;
                    if (_myUnicastListener.HasPendingMessage)
                    {
                        bFoundMessages = true;
                        var msg = _myUnicastListener.AcceptMessage();
                        //if(msg.Tag == _responseTag)
                        //{
                            parent_program.shipIOHandler.Echo("Unicast received. Data: " + msg.Data.ToString());
                        //}
                    }
                } while (bFoundMessages);
            }
        }
    }
}
