using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.IO;

namespace ECDISnmeadatareceiver
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // 멀티캐스트 통신 생성
            UdpClient udpClient1 = CreateMulticastUdpClient(60015);
            UdpClient udpClient2 = CreateMulticastUdpClient(60015);

            // 60015, 60025 멀티캐스트 그룹 등록
            JoinMulticastGroup(udpClient1, "239.192.0.15");
            JoinMulticastGroup(udpClient2, "239.192.0.25");

            // 60015 통신으로 패킷 요청
            // interval에 따라 반복 요청하도록 수정 필요
            SendMulticastUdp("239.192.0.15", 60015, CreateReqECDISDataSentence());

            // 60025 통신으로 패킷 데이터 수신

            // 60015 통신으로 통신 완료 신호 수신



            //LoadUdpMulticastClient(60025, "239.192.0.25"); // ECDIS P450 - Simple binary
            //LoadUdpMulticastClient(60015, "239.192.0.15"); // ECDIS P450 - NMEA
            //LoadUdpUnicastClient(60000, "127.0.0.1"); // Unicast 방식으로는 ECDIS 데이터를 받아올 수 없음
        }

        #region MulticastUdpClient
        // 멀티캐스트 통신 생성
        public UdpClient CreateMulticastUdpClient(int port)
        {
            var udp = new UdpClient();
            udp.ExclusiveAddressUse = false;
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            AddLog($"[Create] UDP Client 포트 {port} 생성 및 바인딩 완료");

            return udp;
        }

        // 멀티캐스트 그룹 등록
        public void JoinMulticastGroup(UdpClient udp, string multicastIP)
        {
            try
            {
                IPAddress ipAddr = IPAddress.Parse(multicastIP);
                udp.JoinMulticastGroup(ipAddr);
                AddLog($"[Join] Multicast Group {multicastIP} 가입 완료");
            }
            catch (Exception ex)
            {
                AddLog($"[Error] 그룹 가입 실패: {ex.Message}");
            }
        }

        // 멀티캐스트 그룹 탈퇴
        public void DropMulticastGroup(UdpClient udp, string multicastIP)
        {
            try
            {
                IPAddress ipAddr = IPAddress.Parse(multicastIP);
                udp.DropMulticastGroup(ipAddr);
                AddLog($"[Join] Multicast Group {multicastIP} 탈퇴 완료");
            }
            catch (Exception ex)
            {
                AddLog($"[Error] 그룹 탈퇴 실패: {ex.Message}");
            }
        }

        // 멀티캐스트 통신 정지
        public void StopMulticastUdpClent(UdpClient udp)
        {
            if (udp != null)
            {
                udp.Close();
                udp.Dispose();
                AddLog("[Close] UdpClient 정지 및 리소스 해제 완료");
            }
        }

        // 멀티캐스트 통신 송신
        public void SendMulticastUdp(string multicastIP, int port, string str)
        {
            byte[] data = Encoding.ASCII.GetBytes(str);
            try
            {
                SendMulticastUdp(multicastIP, port, data);
            }
            catch(Exception ex)
            {
                AddLog($"[Error] 송신 실패: {ex.Message}");
                throw ex;
            }
        }

        public void SendMulticastUdp(string multicastIP, int port, byte[] data)
        {
            try
            {
                using (UdpClient sender = new UdpClient())
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(multicastIP), port);
                    sender.Send(data, data.Length, remoteEP);
                    AddLog($"[Send] {multicastIP}:{port} 송신 완료 ({data.Length} bytes)");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[Error] 송신 실패: {ex.Message}");
                throw ex;
            }
        }

        // 멀티캐스트 통신 수신

        #endregion

        #region 450 Data
        public string MakeUdPbC450Message()
        {
            // UdPbC\s:SM0001,d:EI0001*29\$SMRRT,Q,,,,,*1B
            string msg = "";

            string backslash = "\\";
            string sourceId = "SM0001";
            string destinationId = "EI0001";

            // UdPbC
            // Datagram Header
            msg += "UdPbC";

            // UdPbC\s:SM0001,d:EI0001*29
            // TAG param = Source & Destination Identifier 
            // 나중에 IEC61162-450 기준으로 생성하는 utils 만들기
            string tagBlock = backslash + "s:" + sourceId + ",d:" + destinationId;
            msg += tagBlock + GetChecksum(tagBlock);

            // \$SMRRT,Q,,,,,*1B
            // 요청 nmea sentence 입력
            var sentence = "$SMRRT,Q,,,,,";
            msg += tagBlock + GetChecksum(sentence);

            return msg;
        }

        public byte[] CreateReqECDISDataSentence()
        {
            string msg = MakeUdPbC450Message();
            return Encoding.ASCII.GetBytes(msg);
        }
        #endregion

        #region Nmea Result Class
        public class NmeaSentence
        {
            // $/!
            // *
            // ,
            public string Header { get; set; }
            public string[] Fields { get; set; }
            public byte? Checksum { get; set; }
            public string FullSentence { get; set; }
            public bool IsValid { get; set; }
        }

        public NmeaSentence ParseNmeaSentence(string sentence)
        {
            var result = new NmeaSentence();

            try
            {
                result.IsValid = false;

                // 0. 예외처리
                // 값이 없는 경우
                if (string.IsNullOrWhiteSpace(sentence)) 
                    return result;

                // 센텐스가 $/!로 시작되지 않는 경우
                if (!(sentence.StartsWith("$") || sentence.StartsWith("!"))) 
                    return result;

                // 체크섬 구분자 *이 없는 경우
                if (!sentence.Contains("*")) 
                    return result;

                // 센텐스 Max Length 보다 긴 경우
                //if (sentence.Length > 82) 
                //    return result;

                // 1. $ 제거
                if (sentence.StartsWith("$") || sentence.StartsWith("!")) 
                    sentence = sentence.Substring(1);

                // 2. 체크섬 분리
                string[] parts = sentence.Split('*');
                if (parts.Length != 2) 
                    return result;

                string dataPart = parts[0];
                string checksumStr = parts[1].Trim();

                // 3. 체크섬 파싱 (hex → byte)
                if (!byte.TryParse(checksumStr, System.Globalization.NumberStyles.HexNumber, null, out byte actualChecksum))
                    return result;

                // 4. 체크섬 계산
                byte calcChecksum = CalChecksum(dataPart);

                if (calcChecksum != actualChecksum)
                {
                    Invoke(new Action(() =>
                    {
                        AddLog($"체크섬 불일치");
                    }));
                    return result; // 체크섬 불일치
                }

                Invoke(new Action(() =>
                {
                    AddLog($"체크섬 일치");
                }));

                // 5. 필드 파싱 (',' 기준)
                string[] fields = dataPart.Split(',');

                result.IsValid = true;
                result.Fields = fields;
                result.Checksum = actualChecksum;

                return result;
            }
            catch
            {
                return result; // 모든 예외에 대해 실패 반환
            }
        }
        #endregion

        #region Nmea Sentence Format Loader Class
        public class NmeaSentenceField
        {
            public int no { get; set; }
            public string field { get; set; }
        }

        // 한번만 로딩해서 데이터 매핑 클래스에 포멧을 저장해두는 방식
        public class NmeaSentenceFormatLoader
        {
            public Dictionary<string, List<NmeaSentenceField>> Load(string jsonFilePath = "nmea_sentence_format.json")
            {
                string jsonString = File.ReadAllText(jsonFilePath);
                var sentenceMap = JsonSerializer.Deserialize<Dictionary<string, List<NmeaSentenceField>>>(jsonString);

                if (sentenceMap == null)
                {
                    sentenceMap = new Dictionary<string, List<NmeaSentenceField>>();
                }

                return sentenceMap;
            }
        }
        #endregion

        #region utils
        public string GetChecksum(string str)
        {
            return "*" + CalChecksum(str);
        }

        public byte CalChecksum(string str)
        {
            byte calcChecksum = 0;
            foreach (char c in str)
            {
                calcChecksum ^= (byte)c;
            }

            return calcChecksum;
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

            // 항목 개수가 10000 초과 시, 초과된 만큼 앞에서부터 제거
            int maxLines = 10000;
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
