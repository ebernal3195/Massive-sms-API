using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Envio_sms_masivos.Clases
{
    class RespuestaExito
    {
        public string success { get; set; }
        public string message { get; set; }
        public int status { get; set; }
        public string code { get; set; }
        public IList<Referencias> references { get; set; }

        /*
            {
                "success": true,
                "message": "Mensajes enviados",
                "status": 200,
                "code": "sms_11",
                "references": 
                [
                    {
                        "reference": "20102050eeb3487faUR23v0xvKIOVNsQpo4Mhl6B",
                        "number": 523334971555
                    }
                ]
            }
        */
    }

    class Referencias
    {
        public string reference { get; set; }
        public string number { get; set; }
    }

    class RespuestaFallida
    {
        public string success { get; set; }
        public string message { get; set; }
        public string code { get; set; }
        public string number { get; set; }
        public int status { get; set; }

        /*
            {
                "success": false,
                "message": "Número con formato incorrecto",
                "code": "sms_04",
                "number": "asdasd",
                "status": 200
            }
        */
    }

    
}
