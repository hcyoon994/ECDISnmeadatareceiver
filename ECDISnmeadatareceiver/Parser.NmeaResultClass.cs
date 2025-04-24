using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace ECDISnmeadatareceiver
{
    public partial class Parser : Form
    {

        #region NmeaResultClass
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

                // $ 또는 ! 로 시작하는 위치부터 자르기 (커스텀 prefix 무시)
                int startIndex = sentence.IndexOf('$');
                if (startIndex == -1) startIndex = sentence.IndexOf('!');
                if (startIndex == -1) return result;

                sentence = sentence.Substring(startIndex);

                // 체크섬 구분자 *이 없는 경우
                if (!sentence.Contains("*"))
                    return result;

                Invoke(new Action(() =>
                {
                    AddLog($"Sentence : " + sentence);
                }));

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
                string calcChecksum = CalChecksum(dataPart);

                if (Convert.ToByte(calcChecksum) != actualChecksum)
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

    }
}
