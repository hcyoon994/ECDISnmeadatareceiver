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
            UdpMulticastClient();
            //UdpUnicastClient();
        }

        public void UdpMulticastClient(int listenPort = 60016, string ip = "239.192.0.21")
        {

            var loader = new NmeaSentenceFormatLoader();
            var sentenceMap = loader.Load();

            if (sentenceMap == null)
            {
                listBox1.Items.Add($"Cannot Load 'nmea_sentence_format.json' file");
                // 정지 시 재실행

            }
            listBox1.Items.Add($"NmeaSentence Format Map Load");

            IPAddress connectIp = IPAddress.Parse(ip); // 연결 IP 주소
            //IPAddress connectIp = IPAddress.Any; // 연결 IP 주소
            
            listBox1.Items.Add($"UDP 수신 대기 중... {connectIp} : {listenPort}");


            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, listenPort);
            UdpClient udp = new UdpClient();
            udp.ExclusiveAddressUse = false;
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.ExclusiveAddressUse = false;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));
            //udp.Client.Bind(localEP);

            IPAddress multicastIP = IPAddress.Parse(ip);

            if ((multicastIP.GetAddressBytes().First() & 0xF0) == 0xE0)
            {
                if (string.IsNullOrEmpty(ip))
                {
                    //NIC 하나일때
                    udp.JoinMulticastGroup(multicastIP);
                    listBox1.Items.Add($"JoinMulticastGroup 완료");
                }
                else
                {
                    //NIC복수개여서 어떤 랜포트에 타게팅할지 매개변수 넣어야함 예) ECDIS 192.168.1.20
                    //NIC2와 연결. 디지털브릿지(192.168.1.20) - Ecdis(192.168.1.10)
                    try
                    {
                        udp.JoinMulticastGroup(multicastIP, IPAddress.Parse(ip));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"{connectIp}:{listenPort} connectIp={connectIp}" + ex.Message);
                        udp.JoinMulticastGroup(multicastIP);
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
                        //    byte[] receivedBytes = udpClient.Receive(ref remoteEP);
                        //    string receivedData = Encoding.ASCII.GetString(receivedBytes);

                        //    // UI 스레드에서 컨트롤 접근은 Invoke 필요 
                        //    Invoke(new Action(() =>
                        //    {
                        //        listBox1.Items.Add($"받은 데이터: {receivedData}");
                        //        listBox1.SelectedIndex = listBox1.Items.Count - 1;
                        //    }));

                        byte[] receivedBytes = udp.Receive(ref localEP);
                        string receivedData = Encoding.ASCII.GetString(receivedBytes);

                        //listBox1.Items.Add($"받은 데이터 : {receivedData}");

                        // 받은 nmea 데이터를 처리하는 function 생성
                        NmeaResult nmeaData = ParseNmeaSentence(receivedData);

                        string talkerId = Convert.ToString(nmeaData.Fields[0]).Substring(0, 2);
                        string sentenceId = Convert.ToString(nmeaData.Fields[0]).Substring(2, 3);
                        Invoke(new Action(() =>
                        {
                            listBox1.Items.Add($"받은 데이터 : {receivedData}");
                            listBox1.Items.Add($"TalkerID : {talkerId}");
                            listBox1.Items.Add($"SentenceID : {sentenceId}");
                        }));

                        // field와 값을 매칭
                        if (sentenceMap.TryGetValue(sentenceId, out var fields))
                        {

                            for (int i = 0; i < fields.Count && i < nmeaData.Fields.Length; i++)
                            {
                                Invoke(new Action(() =>
                                {
                                    listBox1.Items.Add($"#{i + 1} - {Convert.ToString(nmeaData.Fields[i + 1])} : {fields[i].field}");
                                    //listBox1.SelectedIndex = listBox1.Items.Count - 1;
                                }));
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Invoke(new Action(() =>
                        {
                            listBox1.Items.Add($"오류: {ex.Message}");
                            listBox1.Items.Add(ex.StackTrace); // 예외 스택 추적 출력
                        }));
                    }
                }
            });

        }

        public void UdpUnicastClient(int listenPort = 60016, string ip = "239.192.0.21")
        {

            var loader = new NmeaSentenceFormatLoader();
            var sentenceMap = loader.Load();

            if (sentenceMap == null)
            {
                listBox1.Items.Add($"Cannot Load 'nmea_sentence_format.json' file");
                // 정지 시 재실행

            }
            listBox1.Items.Add($"NmeaSentence Format Map Load");

            IPAddress connectIp = IPAddress.Parse(ip); // 연결 IP 주소
            //IPAddress connectIp = IPAddress.Any; // 연결 IP 주소

            UdpClient udpClient = new UdpClient(listenPort);
            IPEndPoint remoteEP = new IPEndPoint(connectIp, listenPort);

            listBox1.Items.Add($"UDP 수신 대기 중... {connectIp} : {listenPort}");

            // Task를 사용해 백그라운드에서 실행
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        //    byte[] receivedBytes = udpClient.Receive(ref remoteEP);
                        //    string receivedData = Encoding.ASCII.GetString(receivedBytes);

                        //    // UI 스레드에서 컨트롤 접근은 Invoke 필요 
                        //    Invoke(new Action(() =>
                        //    {
                        //        listBox1.Items.Add($"받은 데이터: {receivedData}");
                        //        listBox1.SelectedIndex = listBox1.Items.Count - 1;
                        //    }));

                        byte[] receivedBytes = udpClient.Receive(ref remoteEP);
                        string receivedData = Encoding.ASCII.GetString(receivedBytes);

                        //listBox1.Items.Add($"받은 데이터 : {receivedData}");

                        // 받은 nmea 데이터를 처리하는 function 생성
                        NmeaResult nmeaData = ParseNmeaSentence(receivedData);

                        string talkerId = Convert.ToString(nmeaData.Fields[0]).Substring(0, 2);
                        string sentenceId = Convert.ToString(nmeaData.Fields[0]).Substring(2, 3);
                        Invoke(new Action(() =>
                        {
                            listBox1.Items.Add($"받은 데이터 : {receivedData}");
                            listBox1.Items.Add($"TalkerID : {talkerId}");
                            listBox1.Items.Add($"SentenceID : {sentenceId}");
                        }));

                        // field와 값을 매칭
                        if (sentenceMap.TryGetValue(sentenceId, out var fields))
                        {

                            for (int i = 0; i < fields.Count && i < nmeaData.Fields.Length; i++)
                            {
                                Invoke(new Action(() =>
                                {
                                    listBox1.Items.Add($"#{i + 1} - {Convert.ToString(nmeaData.Fields[i + 1])} : {fields[i].field}");
                                    listBox1.SelectedIndex = listBox1.Items.Count - 1;
                                }));
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Invoke(new Action(() =>
                        {
                            listBox1.Items.Add($"오류: {ex.Message}");
                            listBox1.Items.Add(ex.StackTrace); // 예외 스택 추적 출력
                        }));
                    }
                }
            });
        }


        #region Nmea Result Class
        public class NmeaResult
        {
            public bool IsValid { get; set; }
            public string[] Fields { get; set; }
            public byte? Checksum { get; set; }
        }

        public NmeaResult ParseNmeaSentence(string sentence)
        {
            var result = new NmeaResult();

            try
            {
                // 0. 예외처리
                // 값이 없는 경우
                // 센텐스가 $/!로 시작되지 않는 경우 (IEC 61162에 따라 수정 가능)
                // 체크섬 구분자 *이 없는 경우
                // 센텐스 Max Length 보다 긴 경우 (IEC 61162에 따라 수정 가능)
                if (string.IsNullOrWhiteSpace(sentence)) return result;
                if (!(sentence.StartsWith("$") || sentence.StartsWith("!"))) return result;
                if (!sentence.Contains("*")) return result;
                if (sentence.Length > 82) return result;

                // 1. $ 제거
                if (sentence.StartsWith("$") || sentence.StartsWith("!")) sentence = sentence.Substring(1);

                // 2. 체크섬 분리
                string[] parts = sentence.Split('*');
                if (parts.Length != 2) return result;

                string dataPart = parts[0];
                string checksumStr = parts[1].Trim();

                // 3. 체크섬 파싱 (hex → byte)
                if (!byte.TryParse(checksumStr, System.Globalization.NumberStyles.HexNumber, null, out byte actualChecksum))
                    return result;

                // 4. 체크섬 계산
                byte calcChecksum = 0;
                foreach (char c in dataPart)
                {
                    calcChecksum ^= (byte)c;
                }

                if (calcChecksum != actualChecksum)
                {
                    Invoke(new Action(() =>
                    {
                        listBox1.Items.Add($"체크섬 불일치");
                    }));
                    return result; // 체크섬 불일치
                }

                Invoke(new Action(() =>
                {
                    listBox1.Items.Add($"체크섬 일치");
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

        #region Nmea Sentence Mapping Class
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
                return sentenceMap ?? new Dictionary<string, List<NmeaSentenceField>>();
            }
        }
        #endregion

    }
}
