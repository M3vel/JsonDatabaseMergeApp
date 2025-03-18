using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonDatabaseMergeApp.Database
{
    public class Sample
    {
        public int Id { get; set; }
        public int SurveyId { get; set; }
        public string Name { get; set; }
        public string ScanTime { get; set; }
        public string DateAdded { get; set; }
        public int Status { get; set; }
        public Dictionary<string, double> Concentrations { get; set; } = new Dictionary<string, double>();
        public string Remarks { get; set; }
        public string MotherLiquorSerialNumber { get; set; }
    }
}
