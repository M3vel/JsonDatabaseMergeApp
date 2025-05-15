using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JsonDatabaseMergeApp.Database
{
    public class Sample
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("survey_id")]
        public int SurveyId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("scan_time")]
        public string ScanTime { get; set; }

        [JsonPropertyName("date_added")]
        public string DateAdded { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("concentrations")]
        public Dictionary<string, double> Concentrations { get; set; }

        [JsonPropertyName("Remarks")]
        public string Remarks { get; set; }

        [JsonPropertyName("Mother liquor serial number")]
        public string MotherLiquorSerialNumber { get; set; }
    }
}
