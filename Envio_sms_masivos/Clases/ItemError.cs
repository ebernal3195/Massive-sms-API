using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Envio_sms_masivos
{
    class ItemError
    {
        public string IdError { get; set; }
        public string Mensaje { get; set; }

        public ItemError(string _IdError, string _Mensaje)
        {
            IdError = _IdError;
            Mensaje = _Mensaje;
        }
    }
}
