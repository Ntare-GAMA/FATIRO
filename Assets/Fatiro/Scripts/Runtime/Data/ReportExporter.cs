using System;
using System.Globalization;
using System.IO;
using System.Text;
using Fatiro.Domain;
using UnityEngine;

namespace Fatiro.Data
{
    public enum ExportFormat
    {
        Json,
        Csv
    }

    /// <summary>
    /// Turns a session into JSON or CSV (one row per load test) and hands it to the Android share
    /// sheet. Nothing leaves the device without that user action (FR-25, NFR-15).
    /// </summary>
    public static class ReportExporter
    {
        public static string Render(SessionRecord record, ExportFormat format) =>
            format == ExportFormat.Json ? JsonUtility.ToJson(record, true) : ToCsv(record);

        public static string ToCsv(SessionRecord record)
        {
            var csv = new StringBuilder();
            csv.AppendLine("session_id,learner,design,design_version,slope_deg,soil,mode,test_time,load_case,magnitude,peak_utilisation,failed_members,runoff_l_per_h,score_total,score_assembly,score_safety,score_cost");
            var score = record.score ?? new ScoreRecord();
            if (record.loadTests.Count == 0)
            {
                AppendRow(csv, record, score, null);
            }
            else
            {
                foreach (var test in record.loadTests) AppendRow(csv, record, score, test);
            }
            return csv.ToString();
        }

        /// <summary>Writes a copy under exports/ and opens the share sheet. Returns the saved path.</summary>
        public static string Export(SessionRecord record, ExportFormat format)
        {
            var text = Render(record, format);
            var extension = format == ExportFormat.Json ? "json" : "csv";
            var directory = Path.Combine(Application.temporaryCachePath, "exports");
            var path = Path.Combine(directory, $"fatiro-{record.design?.designId}-{record.sessionId?.Substring(0, 8)}.{extension}");
            AtomicFile.WriteAllText(path, text);
            Share(text, $"Fatiro AR report – {record.design?.designName} ({record.learnerName})");
            return path;
        }

        private static void Share(string text, string subject)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent");
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), subject);
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share session report");
                activity.Call("startActivity", chooser);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Fatiro] Share failed: {exception.Message}");
            }
#else
            GUIUtility.systemCopyBuffer = text;
            Debug.Log($"[Fatiro] {subject} copied to the clipboard (share sheet is Android only).");
#endif
        }

        private static void AppendRow(StringBuilder csv, SessionRecord record, ScoreRecord score, LoadTestRecord test)
        {
            string Cell(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
            string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

            csv.Append(Cell(record.sessionId)).Append(',')
                .Append(Cell(record.learnerName)).Append(',')
                .Append(Cell(record.design?.designName)).Append(',')
                .Append(record.design?.designVersion ?? 0).Append(',')
                .Append(record.site?.slopeDeg ?? 0).Append(',')
                .Append(Cell(record.site?.soil)).Append(',')
                .Append(Cell(record.site?.mode)).Append(',')
                .Append(Cell(test?.t)).Append(',')
                .Append(Cell(test?.@case)).Append(',')
                .Append(test == null ? string.Empty : Number(test.magnitude)).Append(',')
                .Append(test == null ? string.Empty : Number(test.peakUtilisation)).Append(',')
                .Append(Cell(test == null ? string.Empty : string.Join(" ", test.failedMembers))).Append(',')
                .Append(test == null ? string.Empty : Number(test.runoffLitresPerHour)).Append(',')
                .Append(score.total).Append(',')
                .Append(score.assembly).Append(',')
                .Append(score.safety).Append(',')
                .Append(score.cost)
                .AppendLine();
        }
    }
}
