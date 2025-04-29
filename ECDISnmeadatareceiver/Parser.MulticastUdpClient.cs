using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ECDISnmeadatareceiver
{
    public partial class Parser : Form
    {

        #region MulticastUdpClient
        public void Create60015UdpClient()
        {
            if (udp60015 == null)
            {
                udp60015 = new UdpClient();
                udp60015.ExclusiveAddressUse = false;
                udp60015.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp60015.Client.Bind(new IPEndPoint(IPAddress.Any, 60015));
                udp60015.JoinMulticastGroup(IPAddress.Parse("239.192.0.15"));

                StartReceiveLoop(udp60015, 60015);
                AddLog("[60015] 멀티캐스트 그룹 가입 완료");
            }
            else
            {
                AddLog("[60015] 이미 멀티캐스트 그룹에 가입되어 있음");
            }
        }

        public void Create60025UdpClient()
        {
            if (udp60025 == null)
            {
                udp60025 = new UdpClient();
                udp60025.ExclusiveAddressUse = false;
                udp60025.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp60025.Client.Bind(new IPEndPoint(IPAddress.Any, 60025));
                udp60025.JoinMulticastGroup(IPAddress.Parse("239.192.0.25"));

                StartReceiveLoop(udp60025, 60025);
                AddLog("[60025] 멀티캐스트 그룹 가입 완료");
            }
            else
            {
                AddLog("[60025] 이미 멀티캐스트 그룹에 가입되어 있음");
            }
        }

        public void Create60002UdpClient()
        {
            if (udp60002 == null)
            {
                udp60002 = new UdpClient();
                udp60002.ExclusiveAddressUse = false;
                udp60002.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp60002.Client.Bind(new IPEndPoint(IPAddress.Any, 60002));
                udp60002.JoinMulticastGroup(IPAddress.Parse("239.192.0.2"));

                StartReceiveLoop(udp60002, 60002);
                AddLog("[60002] 멀티캐스트 그룹 가입 완료");
            }
            else
            {
                AddLog("[60002] 이미 멀티캐스트 그룹에 가입되어 있음");
            }
        }

        void StartReceiveLoop(UdpClient client, int portNo)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var result = await client.ReceiveAsync();
                        string msg = Encoding.ASCII.GetString(result.Buffer);
                        AddLog($"[Receive:{portNo}] {msg}");

                        string header = Encoding.ASCII.GetString(result.Buffer.Take(5).ToArray());

                        if (header == "RaUdP")
                            ProcessRtz(result.Buffer);
                        else if (header == "UdPbC")
                            ProcessNmea(result.Buffer);
                        else
                            ProcessNmea(result.Buffer);
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[Error:{portNo}] {ex.Message}");
                    }
                }
            });
            Task.Delay(5000);
        }

        // 멀티캐스트 통신 송신
        public void SendMulticastUdpFrom60015(string multicastIP, int port, string str)
        {
            byte[] data = Encoding.ASCII.GetBytes(str);
            SendMulticastUdpFrom60015(multicastIP, port, data);
        }

        public void SendMulticastUdpFrom60015(string multicastIP, int port, byte[] data)
        {
            try
            {
                if (udp60015 == null)
                {
                    AddLog("[Error] udp60015가 초기화되지 않았습니다.");
                    return;
                }

                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(multicastIP), port);
                udp60015.Ttl = 16;
                udp60015.Send(data, data.Length, remoteEP);
                AddLog($"[Send] {multicastIP}:{port} 송신 완료 ({data.Length} bytes)");
            }
            catch (Exception ex)
            {
                AddLog($"[Error] 송신 실패: {ex.Message}");
                throw;
            }
        }

        #endregion

    }
}
