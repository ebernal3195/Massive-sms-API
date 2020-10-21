using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Envio_sms_masivos
{
    class Cobro
    {
        public int Indice { get; set; }
        public bool EnviarSMS { get; set; }
        public string Contrato { get; set; }
        public decimal Saldo { get; set; }
        public string Recibo { get; set; }
        public decimal Monto { get; set; }
        public string Cliente { get; set; }
        public string Celular { get; set; }
        public string Cobrador { get; set; }
        public DateTime Fecha_hora_evento { get; set; }
        public int Estatus_cobro { get; set; }
        public string No_cobro { get; set; }
        public string ResultadoEnvio { get; set; }
        public string DescripciónResultado { get; set; }
        public string referencia;

        public Cobro()
        {
            EnviarSMS = true;
        }

        public Cobro(bool _enviarSMS, int _indice, string _contrato, decimal _saldo, string _recibo, decimal _monto, string _cliente, string _celular, string _cobrador, DateTime _fecha_hora_evento, int _estatus_cobro, string _no_cobro)
        {
            Indice = _indice;
            EnviarSMS = _enviarSMS;
            Contrato = _contrato;
            Saldo = _saldo;
            Recibo = _recibo;
            Monto = _monto;
            Cliente = _cliente;
            Celular = _celular;
            Cobrador = _cobrador;
            Fecha_hora_evento = _fecha_hora_evento;
            Estatus_cobro = _estatus_cobro;
            No_cobro = _no_cobro;
            ResultadoEnvio = "0";
            DescripciónResultado = "Descripción por defecto";
            referencia = string.Empty;
        }
    }
}
