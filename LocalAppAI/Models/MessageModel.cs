using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalAppAI.Models
{
    public class MessageModel
    {
        public int ID { get; set; }
        public string Content { get; set; }
        public int TokensGenerated { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsFromUser { get; set; }
    }
}
