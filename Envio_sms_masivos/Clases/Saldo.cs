using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Envio_sms_masivos.Clases
{
    class Saldo
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int Status { get; set; }
        public string Code { get; set; }
        public int Credit { get; set; }
    }
}
