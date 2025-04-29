using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows.Forms;
using System.Xml.Linq;

namespace ECDISnmeadatareceiver
{
    public partial class Parser : Form
    {
        #region RTZ
        public class RtzAssembly
        {
            public List<byte[]> RtzChunks { get; set; } = new List<byte[]>();
            public string Bid { get; set; } = null;
            public DateTime LastUpdated { get; set; } = DateTime.Now;

            public bool IsReadyToSave => RtzChunks.Count > 0 && !string.IsNullOrEmpty(Bid);
        }

        RtzAssembly currentAssembly = null;
        System.Timers.Timer rtzTimeoutTimer = null;
        readonly TimeSpan AssemblyTimeout = TimeSpan.FromSeconds(10); // 10초 내에 받아오는 RTZ 데이터를 추출함
        string saveFolder = Path.Combine(Application.StartupPath, "SavedRTZ");

        // RTZ 수신
        public void ProcessRtz(byte[] data)
        {
            if (data.Length <= 46)
            {
                AddLog("[RTZ] 데이터가 너무 짧음");
                return;
            }

            byte[] xmlData = data.Skip(46).ToArray();

            if (currentAssembly == null)
                currentAssembly = new RtzAssembly();

            currentAssembly.RtzChunks.Add(xmlData);
            currentAssembly.LastUpdated = DateTime.Now;

            StartOrResetTimeoutTimer();
        }

        // NMEA 수신
        public void ProcessNmea(byte[] data)
        {
            string message = Encoding.ASCII.GetString(data);

            if (!message.Contains("$EIRRT"))
            {
                NmeaMapping(message);
                return;
            }

            string cleaned = message.Replace("*", ",");

            var match = Regex.Match(cleaned, @"bid(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                AddLog("[NMEA] bid 찾기 실패");
                return;
            }

            string bid = match.Groups[1].Value;
            AddLog($"[NMEA] bid 추출 완료: {bid}");

            if (currentAssembly == null)
                currentAssembly = new RtzAssembly();

            currentAssembly.Bid = bid;
        }

        public void StartOrResetTimeoutTimer()
        {
            if (rtzTimeoutTimer == null)
            {
                rtzTimeoutTimer = new System.Timers.Timer(AssemblyTimeout.TotalMilliseconds);
                rtzTimeoutTimer.Elapsed += OnTimeoutElapsed;
                rtzTimeoutTimer.AutoReset = false;
            }

            rtzTimeoutTimer.Stop();
            rtzTimeoutTimer.Start();
        }

        public void OnTimeoutElapsed(object sender, ElapsedEventArgs e)
        {
            SaveCurrentAssembly();
        }

        public void SaveCurrentAssembly()
        {
            if (currentAssembly == null || currentAssembly.RtzChunks.Count == 0)
            {
                AddLog("[Save] 저장할 데이터 없음");
                return;
            }

            Directory.CreateDirectory(saveFolder);

            byte[] merged = MergeChunks(currentAssembly.RtzChunks);

            string bidPart = string.IsNullOrEmpty(currentAssembly.Bid) ? "NO_BID" : currentAssembly.Bid;
            string timestamp = currentAssembly.LastUpdated.ToString("yyyyMMdd_HHmmss");
            string filename = $"{timestamp}_{bidPart}.txt";
            string fullPath = Path.Combine(saveFolder, filename);

            try
            {
                File.WriteAllBytes(fullPath, merged);
                AddLog($"[Save] RTZ 저장 완료: {fullPath} ({merged.Length} bytes)");

                // RTZ 데이터 파일을 읽고 매핑
                //var waypoints = ExtractWaypoints($"{fullPath}");
                //foreach (var wp in waypoints)
                //{
                //    AddLog($"Waypoint ID={wp.Id} / Lat={wp.Latitude} / Lon={wp.Longitude}");
                //}
            }
            catch (Exception ex)
            {
                AddLog($"[Error] 저장 실패: {ex.Message}");
            }

            currentAssembly = null;
            rtzTimeoutTimer?.Stop();
        }

        public byte[] MergeChunks(List<byte[]> chunks)
        {
            using (var ms = new MemoryStream())
            {
                foreach (var chunk in chunks)
                    ms.Write(chunk, 0, chunk.Length);
                return ms.ToArray();
            }
        }
        #endregion

        #region Waypoint
        public class Waypoint
        {
            public string Id { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        public List<Waypoint> ExtractWaypoints(string filePath)
        {
            List<Waypoint> waypoints = new List<Waypoint>();

            try
            {
                string xmlText = File.ReadAllText(filePath);

                // "route" 태그가 시작하는 위치를 찾는다
                int routeStartIndex = xmlText.IndexOf("<route");
                if (routeStartIndex == -1)
                {
                    throw new Exception("RTZ 파일에 <route> 태그가 없습니다!");
                }

                // "<route"부터 끝까지 추출
                string cleanXml = xmlText.Substring(routeStartIndex);

                // 이제 이걸 파싱하면 됨
                XDocument doc = XDocument.Parse(cleanXml);

                var waypointElements = doc.Descendants().Where(e => e.Name.LocalName == "waypoint");

                foreach (var wp in waypointElements)
                {
                    var position = wp.Element(wp.Name.Namespace + "position");
                    if (position == null) continue;

                    string id = wp.Attribute("id")?.Value ?? "unknown";
                    double lat = double.Parse(position.Attribute("lat")?.Value ?? "0");
                    double lon = double.Parse(position.Attribute("lon")?.Value ?? "0");

                    waypoints.Add(new Waypoint
                    {
                        Id = id,
                        Latitude = lat,
                        Longitude = lon
                    });
                }
            }
            catch (Exception ex)
            {
                AddLog($"[Error] Waypoint 추출 실패: {ex.Message}");
            }

            return waypoints;
        }

        #endregion
    }
}
