using System;
using System.Collections.Generic;
using System.IO;
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
        UdpClient udp60002;
        UdpClient udp60015;
        UdpClient udp60025;

        public Parser()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //LoadUdpMulticastClient(60025, "239.192.0.25"); // ECDIS P450 - Simple binary
            //LoadUdpMulticastClient(60015, "239.192.0.15"); // ECDIS P450 - NMEA
            //LoadUdpUnicastClient(60000, "127.0.0.1"); // Unicast 방식으로는 ECDIS 데이터를 받아올 수 없음
        }

        // 60015 멀티캐스트 그룹 등록
        private void button1_Click(object sender, EventArgs e)
        {
            Create60002UdpClient(); // AIS
            //Create60015UdpClient(); // route(RTZ)
            //Create60025UdpClient(); // route(RTZ)
        }

        // 60025 멀티캐스트 그룹 등록
        private void button2_Click(object sender, EventArgs e)
        {

        }

        // 60015 통신으로 패킷 요청
        private void button3_Click(object sender, EventArgs e)
        {
            // 예시: 요청 메시지 정의
            byte[] requestData = CreateReqECDISDataSentence();

            SendMulticastUdpFrom60015("239.192.0.15", 60015, requestData);
            AddLog("[Send] 60015로 요청 메시지 전송 완료");
        }

        #region utils
        public string GetChecksum(string str)
        {
            return "*" + CalChecksum(str);
        }

        public string CalChecksum(string str)
        {
            int checksum = str.First();

            for (int i = 1; i < str.Length; i++)
            {
                // No. XOR the checksum with this character's value
                checksum ^= Convert.ToByte(str[i]);
            }
            // Return the checksum formatted as a two-character hexadecimal
            return checksum.ToString("X2");
        }

        public string MakeUdPbC450Message()
        {
            // UdPbC\s:SM0001,d:EI0001*29\$SMRRT,Q,,,,,*1B
            string msg = "";

            string backslash = "\\";
            string sourceId = "SM0001";
            string destinationId = "EI0001";
            string postFix = "\r\n";

            // ex : UdPbC \s:SM0001,d:EI0001*29\$SMRRT,Q,,,,,*1B<CR><LF>
            // UdPbC
            // Datagram Header
            msg += "UdPbC" + (char)0;

            // UdPbC \s:SM0001,d:EI0001*29
            // TAG param = Source & Destination Identifier 
            // 나중에 IEC61162-450 기준으로 생성하는 utils 만들기
            string tagBlock = "s:" + sourceId + ",d:" + destinationId;
            msg += backslash + tagBlock + GetChecksum(tagBlock);

            // \$SMRRT,Q,,,,,*1B
            // 요청 nmea sentence 입력
            var sentence = "SMRRT,Q,,,,,";
            msg += backslash + "$" + sentence + GetChecksum(sentence) + postFix;

            return msg;
        }

        public byte[] CreateReqECDISDataSentence()
        {
            string msg = MakeUdPbC450Message();
            return Encoding.ASCII.GetBytes(msg);
        }

        public void AddLog(string message)
        {
            if (listBox1.InvokeRequired)
            {
                listBox1.Invoke(new Action(() => AddLog(message)));
                return;
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logLine = $"[{timestamp}] {message}";

            // 리스트박스에 로그 추가
            listBox1.Items.Add(logLine);

            // 항목 개수가 50000 초과 시, 초과된 만큼 앞에서부터 제거
            int maxLines = 50000;
            while (listBox1.Items.Count > maxLines)
            {
                listBox1.Items.RemoveAt(0);
            }


            // 로그 파일 저장
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                string logFileName = DateTime.Now.ToString("yyyy-MM-dd") + ".log";
                string logFilePath = Path.Combine(logDir, logFileName);

                File.AppendAllText(logFilePath, logLine + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // 로그 저장 실패는 UI에만 남김
                listBox1.Items.Add($"[LogError] {ex.Message}");
            }

            // 항상 마지막 항목이 선택되게 (자동 스크롤)
            //listBox1.TopIndex = listBox1.Items.Count - 1;
        }

        #endregion

        #region Not In Use
        // 사용 X
        public void LoadUdpMulticastClient(int listenPort, string ip)
        {
            var loader = new NmeaSentenceFormatLoader();
            var sentenceMap = loader.Load();

            if (sentenceMap == null)
                AddLog($"Cannot Load 'nmea_sentence_format.json' file");
            else
                AddLog($"NmeaSentence Format Map Load");

            // 연결 IP 주소
            IPAddress connectIp = IPAddress.Parse(ip);
            //IPAddress connectIp = IPAddress.Any;

            AddLog($"UDP Multicast 수신 대기 중... {connectIp} : {listenPort}");

            // Multicast Join
            IPEndPoint localEP = new IPEndPoint(connectIp, listenPort);
            UdpClient udp = new UdpClient();
            udp.ExclusiveAddressUse = false;
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.ExclusiveAddressUse = false;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));

            IPAddress multicastIP = IPAddress.Parse(ip);

            if ((multicastIP.GetAddressBytes().First() & 0xF0) == 0xE0)
            {
                if (string.IsNullOrEmpty(ip))
                {
                    // NIC 하나일때
                    udp.JoinMulticastGroup(multicastIP);
                    AddLog($"JoinMulticastGroup 완료");
                }
                else
                {
                    // NIC 복수개
                    try
                    {
                        udp.JoinMulticastGroup(multicastIP, IPAddress.Parse(ip));
                        AddLog($"JoinMulticastGroup 완료");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"{connectIp}:{listenPort} connectIp={connectIp}" + ex.Message);
                        udp.JoinMulticastGroup(multicastIP);
                        AddLog($"JoinMulticastGroup 완료");
                    }
                }
            }

            // Task를 사용해 백그라운드에서 실행
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        byte[] receivedBytes = udp.Receive(ref localEP);
                        string receivedData = Encoding.ASCII.GetString(receivedBytes);

                        Invoke(new Action(() =>
                        {
                            AddLog($"-----Multicast Begin-----");
                            AddLog($"받은 데이터 : {receivedData}");
                        }));

                        // 받은 데이터를 RAW 파일로 저장

                        // 받은 nmea 데이터를 처리
                        NmeaSentence nmeaData = ParseNmeaSentence(receivedData);

                        if (nmeaData.IsValid)
                        {
                            string talkerId = Convert.ToString(nmeaData.Fields[0]).Substring(0, 2);
                            string sentenceId = Convert.ToString(nmeaData.Fields[0]).Substring(2, 3);
                            Invoke(new Action(() =>
                            {
                                AddLog($"TalkerID : {talkerId}");
                                AddLog($"SentenceID : {sentenceId}");
                            }));

                            // field와 값을 매칭
                            if (sentenceMap.TryGetValue(sentenceId, out List<NmeaSentenceField> fields))
                            {
                                for (int i = 0; i < fields.Count && i < nmeaData.Fields.Length; i++)
                                {
                                    Invoke(new Action(() =>
                                    {
                                        AddLog($"Data Field #{i + 1} - {fields[i].field} : {Convert.ToString(nmeaData.Fields[i + 1])}");
                                        //listBox1.SelectedIndex = listBox1.Items.Count - 1;
                                    }));
                                }
                            }
                        }
                        else
                        {
                            Invoke(new Action(() =>
                            {
                                AddLog($"데이터 파싱 오류");
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Invoke(new Action(() =>
                        {
                            AddLog($"오류: {ex.Message}");
                            AddLog(ex.StackTrace); // 예외 스택 추적 출력
                        }));
                    }
                    finally
                    {
                        Invoke(new Action(() =>
                        {
                            AddLog($"-----Multicast Finish-----");
                        }));
                    }
                }
            });
        }

        public void LoadUdpUnicastClient(int listenPort, string ip)
        {
            var loader = new NmeaSentenceFormatLoader();
            var sentenceMap = loader.Load();

            if (sentenceMap == null)
                AddLog($"Cannot Load 'nmea_sentence_format.json' file");
            else
                AddLog($"NmeaSentence Format Map Load");

            IPAddress connectIp = IPAddress.Parse(ip); // 연결 IP 주소
            //IPAddress connectIp = IPAddress.Any; // 연결 IP 주소

            UdpClient udpClient = new UdpClient(listenPort);
            IPEndPoint remoteEP = new IPEndPoint(connectIp, listenPort);

            AddLog($"UDP Unicast 수신 대기 중... {connectIp} : {listenPort}");

            // Task를 사용해 백그라운드에서 실행
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        byte[] receivedBytes = udpClient.Receive(ref remoteEP);
                        string receivedData = Encoding.ASCII.GetString(receivedBytes);

                        Invoke(new Action(() =>
                        {
                            AddLog($"-----Unicast Begin-----");
                            AddLog($"받은 데이터 : {receivedData}");
                        }));

                        // 받은 nmea 데이터를 처리
                        NmeaSentence nmeaData = ParseNmeaSentence(receivedData);

                        if (nmeaData.IsValid)
                        {
                            string talkerId = Convert.ToString(nmeaData.Fields[0]).Substring(0, 2);
                            string sentenceId = Convert.ToString(nmeaData.Fields[0]).Substring(2, 3);

                            Invoke(new Action(() =>
                            {
                                AddLog($"TalkerID : {talkerId}");
                                AddLog($"SentenceID : {sentenceId}");
                            }));

                            // field와 값을 매칭
                            if (sentenceMap.TryGetValue(sentenceId, out List<NmeaSentenceField> fields))
                            {

                                for (int i = 0; i < fields.Count && i < nmeaData.Fields.Length; i++)
                                {
                                    Invoke(new Action(() =>
                                    {
                                        AddLog($"Data Field #{i + 1} - {fields[i].field} : {Convert.ToString(nmeaData.Fields[i + 1])}");
                                        //listBox1.SelectedIndex = listBox1.Items.Count - 1;
                                    }));
                                }
                            }
                        }
                        else
                        {
                            Invoke(new Action(() =>
                            {
                                AddLog($"데이터 파싱 오류");
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Invoke(new Action(() =>
                        {
                            AddLog($"오류: {ex.Message}");
                            AddLog(ex.StackTrace); // 예외 스택 추적 출력
                        }));
                    }
                    finally
                    {
                        Invoke(new Action(() =>
                        {
                            AddLog($"-----Unicast Finish-----");
                        }));
                    }
                }
            });
        }
        #endregion

    }
}
