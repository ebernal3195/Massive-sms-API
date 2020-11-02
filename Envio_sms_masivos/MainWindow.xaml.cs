using Envio_sms_masivos.Clases;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Envio_sms_masivos
{
    static class Bitacora
    {
        public static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger(); //Objeto para generar bitácora
    }

    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Configuración inicial

        private List<ItemError> Lista_errores = new List<ItemError>();
        private List<Cobro> listaCobros = new List<Cobro>();
        private List<Cobro> listaActualizacionWeb = new List<Cobro>();

        private string Parametros_disponibles;
        private string Plantilla_mensaje;
        private int Cantidad_caracteres;
        private decimal segundos_sleep_envio_sms;
        private decimal Segundos_sleep_actualizacion_ecobro;

        bool ContinuarActualizandoEcobro;
        bool ContinuarEnviandoMensajes;

        string WSActualizarCobro;
        string Sandbox;
        string Apikey;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //Verifica si ya existe una instancia del programa ejecutándose
                bool ProgramaEnEjecucion = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1;

                if (ProgramaEnEjecucion)
                {
                    Bitacora.Logger.Info("No se inició el programa, ya existe una instancia en ejecución");
                    Application.Current.Shutdown();
                }
                else
                {
                    Bitacora.Logger.Info("Se inicia programa");

                    if (Cargar_configuracion())
                    {
                        if (Sandbox == "1")
                        {
                            Bitacora.Logger.Info("Ejecución en modo de pruebas (el mensaje no llegará al celular ni se descontará del saldo)");
                        }

                        if (Properties.Settings.Default.Ejecucion_automatica)
                        {
                            Bitacora.Logger.Info("Comienza ejecución en modo automático");

                            if (Consultar_cobros())
                            {
                                ComenzarEnvio();
                            }
                            else
                            {
                                Application.Current.Shutdown();
                            }

                            Application.Current.Shutdown();
                        }
                        else
                        {
                            Bitacora.Logger.Info("Comienza ejecución en modo no automático");
                        }
                    }
                    else
                    {
                        Application.Current.Shutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                Bitacora.Logger.Info("Error en metodo Window_Loaded: " + ex.ToString());
                Bitacora.Logger.Fatal(ex, "Error en metodo Window_Loaded");

                Application.Current.Shutdown();
            }
        }

        private bool Cargar_configuracion()
        {
            try
            {
                //Cargar lista de errores de la API
                Lista_errores = new List<ItemError>();
                ItemError error = new ItemError("auth_01", "Usuario no autorizado"); Lista_errores.Add(error);
                error = new ItemError("sms_01", "Mensaje indefinido"); Lista_errores.Add(error);
                error = new ItemError("sms_02", "Mensaje muy largo"); Lista_errores.Add(error);
                error = new ItemError("sms_03", "Número indefinido"); Lista_errores.Add(error);
                error = new ItemError("sms_04", "Número con formato incorrecto"); Lista_errores.Add(error);
                error = new ItemError("sms_05", "Código de país indefinido"); Lista_errores.Add(error);
                error = new ItemError("sms_06", "Nombre muy largo"); Lista_errores.Add(error);
                error = new ItemError("sms_07", "Créditos insuficientes"); Lista_errores.Add(error);
                error = new ItemError("sms_12", "Error al enviar los mensajes"); Lista_errores.Add(error);
                error = new ItemError("sms_13", "Error al enviar los mensajes"); Lista_errores.Add(error);
                error = new ItemError("sms_15", "Máximo 500 mensajes por envío"); Lista_errores.Add(error);
                error = new ItemError("sms_16", "Número repetido"); Lista_errores.Add(error);
                error = new ItemError("sms_17", "Máximo 1,000 envíos por día en modo de pruebas"); Lista_errores.Add(error);
                error = new ItemError("sms_18", "Código de país no soportado"); Lista_errores.Add(error);

                //Cargar variables de usuario
                Parametros_disponibles = Properties.Settings.Default.Parametros_disponibles;
                Plantilla_mensaje = Properties.Settings.Default.Plantilla_mensaje;
                Cantidad_caracteres = Properties.Settings.Default.CantidadCaracteres;
                segundos_sleep_envio_sms= Properties.Settings.Default.SegundosSleepEnviarMensaje;
                Segundos_sleep_actualizacion_ecobro = Properties.Settings.Default.SegundosSleepActualizacion;
                WSActualizarCobro = Properties.Settings.Default.WSActualizarCobro;
                Sandbox = Properties.Settings.Default.Sandbox;

                //Api key del portal smsmasivos de la cuenta proyectosit@pabsmr.org
                Apikey = Properties.Settings.Default.ApiKey;

                Bitacora.Logger.Info("Se cargó la configuración");
                return true;
            }
            catch (Exception ex)
            {
                Bitacora.Logger.Info("Error al cargar configuracion: " + ex.ToString());
                Bitacora.Logger.Fatal(ex, "Error al cargar configuracion");

                return false;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Bitacora.Logger.Error("Se termina programa" + Environment.NewLine + "######################################################################################" + Environment.NewLine);
        }

        #endregion

        #region Consulta de cobros

        private void btnConsultarCobros_Click(object sender, RoutedEventArgs e)
        {
            Consultar_cobros();
            ComenzarEnvio();
        }

        private bool Consultar_cobros()
        {
            try
            {
                Bitacora.Logger.Info("Comienza consulta de cobros");

                DataTable TablaCobros = new DataTable();
                //Insertar cobros obtenidos en una lista
                Cobro _cobro;

                listaActualizacionWeb = new List<Cobro>();

                //Consulta a web service para obtener cobros
                var client = new RestClient(Properties.Settings.Default.WSConsultaCobros);
                var request = new RestRequest(Method.GET);
                IRestResponse response = client.Execute(request);

                //Adecuar Json de respuesta para serializarlo. Corta la cadena al nivel del los objetos [{ob1, obj2}]
                string json = response.Content;
                int inicio_arreglo = json.IndexOf("[");
                int fin_arreglo = json.IndexOf("]");

                //Manejar Json para cuando no haya cobros
                if (fin_arreglo - inicio_arreglo >= 3)
                {
                    json = json.Remove(0, inicio_arreglo);

                    fin_arreglo = json.IndexOf("]");
                    json = json.Remove(fin_arreglo + 1, (json.Length - fin_arreglo - 1));

                    TablaCobros = JsonConvert.DeserializeObject<DataTable>(json);

                    if (TablaCobros.Rows == null)
                    {
                        throw new Exception("No se obtuvieron cobros, revisar el web service");
                    }
                }

                //############################################################
                //                      TEST
                //############################################################
                //string json = @"
                //        {
                //          ""result"": [
                //            {
                //              ""Contrato"": ""1CF030347"",
                //              ""saldo"": ""8100.00"",
                //              ""abonado"": ""8800.00"",
                //              ""Recibo"": ""1L,40295"",
                //              ""Monto"": ""50"",
                //              ""Cliente"": ""ARTURO ELIGIO MARTINEZ"",
                //              ""Cobrador"": ""IVAN ALEJANDRO HERRERA VEGA"",
                //              ""Fecha_hora_evento"": ""2020-05-21 07:50:53"",
                //              ""Estatus_cobro"": ""1"",
                //              ""No_cobro"": ""34692360"",
                //              ""sms_enviado"": ""0"",
                //              ""ClienteDetalles_FechaTabla"": ""2020-06-18 08:20:42"",
                //              ""Celular"": ""3334971555""
                //            },
                //            {
                //              ""Contrato"": ""1AJ089633"",
                //              ""saldo"": ""11100.00"",
                //              ""abonado"": ""8800.00"",
                //              ""Recibo"": ""14C4055"",
                //              ""Monto"": ""100"",
                //              ""Cliente"": ""JORGE RICARDO ALVAREZ HERNANDEZ"",
                //              ""Cobrador"": ""DANIEL VELAZCO GARCIA"",
                //              ""Fecha_hora_evento"": ""2020-05-21 18:51:54"",
                //              ""Estatus_cobro"": ""1"",
                //              ""No_cobro"": ""34692362"",
                //              ""sms_enviado"": ""0"",
                //              ""ClienteDetalles_FechaTabla"": ""2020-06-16 20:07:10"",
                //              ""Celular"": ""qweqweqweq""
                //            },
                //            {
                //              ""Contrato"": ""2AJ033209"",
                //              ""saldo"": ""22900.00"",
                //              ""abonado"": ""1000.00"",
                //              ""Recibo"": ""E1160793"",
                //              ""Monto"": ""100"",
                //              ""Cliente"": ""JULIO CESAR HERNANDEZ MORALES"",
                //              ""Cobrador"": ""MARCO ANTONIO RIVAS MORALES"",
                //              ""Fecha_hora_evento"": ""2020-10-28 09:55:22"",
                //              ""Estatus_cobro"": ""1"",
                //              ""No_cobro"": ""72565042"",
                //              ""sms_enviado"": ""0"",
                //              ""ClienteDetalles_FechaTabla"": ""2020-07-31 11:13:03"",
                //              ""Celular"": ""3330211967""
                //            }
                //          ]
                //        }";

                ////Recorta el Json al nivel de los objetos
                //int inicio_arreglo = json.IndexOf("[");
                //json = json.Remove(0, inicio_arreglo);
                //int fin_arreglo = json.IndexOf("]");
                //json = json.Remove(fin_arreglo + 1, (json.Length - fin_arreglo - 1));
                //TablaCobros = JsonConvert.DeserializeObject<DataTable>(json);

                //------------------------------------------------------------
                //                      TEST
                //------------------------------------------------------------

                int indice = 1;

                foreach (DataRow fila in TablaCobros.Rows)
                {
                    _cobro = new Cobro(true,
                                        indice,
                                        fila["Contrato"].ToString(),
                                        Convert.ToDecimal(fila["Saldo"]),
                                        fila["Recibo"].ToString(),
                                        Convert.ToDecimal(fila["Monto"]),
                                        fila["Cliente"].ToString(),
                                        fila["Celular"].ToString(),
                                        fila["Cobrador"].ToString(),
                                        Convert.ToDateTime(fila["Fecha_hora_evento"]),
                                        Convert.ToInt32(fila["Estatus_cobro"]),
                                        fila["No_cobro"].ToString()
                                    );

                    listaCobros.Add(_cobro);

                    indice = indice + 1;
                }

                TablaCobros.Clear();

                Bitacora.Logger.Info("Cobros obtenidos: " + listaCobros.Count());

                return true;
            }
            catch (Exception ex)
            {
                Bitacora.Logger.Info("Error al consultar cobros: " + ex.ToString());
                Bitacora.Logger.Fatal(ex, "Error al consultar cobros");

                return false;
            }
        }

        #endregion

        #region Envio de mensajes

        /// <summary>
        /// Obtiene la cantidad de saldo para envio de mensajes 
        /// </summary>
        /// <returns> true si hay saldo</returns>
        private bool SaldoDisponible()
        {
            try
            {
                var client = new RestClient("https://api.smsmasivos.com.mx/credits/consult");
                var request = new RestRequest(Method.POST);
                request.AddHeader("apikey", Apikey);
                //request.AddCookie("__cfduid", "dd0b849de0087cd854583849abd8abd6c1603214316");
                //request.AddCookie("connect.sid", "s%3A2eOa5_GV3EQs5m2GK0pzLrKYa_97bRTf.bsBtTbXEMaxibMe14eIdrCX%2FxmN6qrpUBpzF1X3DWTY");
                IRestResponse response = client.Execute(request);

                Saldo miSaldo = new Saldo();

                miSaldo = JsonConvert.DeserializeObject<Saldo>(response.Content);

                if (miSaldo.Credit == 0)
                {
                    Bitacora.Logger.Info("Se terminó el saldo");
                    Bitacora.Logger.Fatal("Se terminó el saldo");

                    return false;
                }
                else
                {
                    Bitacora.Logger.Info("Saldo disponible: " + miSaldo.Credit.ToString());

                    return true;
                }
            }
            catch (Exception ex)
            {
                Bitacora.Logger.Info("Error al consultar saldo: " + ex.ToString());
                Bitacora.Logger.Fatal(ex, "Error al consultar saldo");

                return false;
            }
        }

        /// <summary>
        /// Comienza el proceso de envio de mensajes
        /// </summary>
        private void ComenzarEnvio()
        {
            if (listaCobros.Count > 0)
            {
                if (!SaldoDisponible())
                {
                    return;
                }


                Thread hiloActualizacionCobro;
                ContinuarActualizandoEcobro = true;

                //Instancia e inicia un hilo que marcará los cobros que ya fueron actualizados
                hiloActualizacionCobro = new Thread(() => RevisarListaActualizacion());
                hiloActualizacionCobro.Start();

                ContinuarEnviandoMensajes = true;

                EscribirLinea();

                foreach (Cobro _cobro in listaCobros)
                {
                    if (ContinuarEnviandoMensajes)
                    {
                        EnviarSMS(_cobro);
                    }
                    else
                    {
                        break;
                    }
                }

                Bitacora.Logger.Info("Se termina proceso de envio");

                //Indica al hilo que actualiza los cobros que se detenga
                ContinuarActualizandoEcobro = false;

                //No permite al programa continuar su ejecución hasta que el hilo que actualiza termine
                hiloActualizacionCobro.Join();
            }
            else
            {
                Bitacora.Logger.Info("No existen cobros pendientes de enviar mensaje");
            }

            //Borra las listas y las cajas de texto
            limpiarAlmacenamiento();
        }

        /// <summary>
        /// Ejecuta el proceso de envio de mensajes, que consiste en:
        /// 1. Verificar celular
        /// 2. Construir mensaje
        /// 3. Enviar mensaje
        /// 4. Ingresar Cobro a lista de actualizacion
        /// </summary>
        private void EnviarSMS(Cobro _cobro)
        {
            string celular;
            string mensaje;

            Bitacora.Logger.Info($"{_cobro.Indice}. {_cobro.Recibo} -> {_cobro.Celular}");

            //VALIDAR CELULAR
            try
            {
                celular = ValidarCelular(_cobro.Celular);

                if (celular == string.Empty)
                {
                    Bitacora.Logger.Info($"Error, numero de celular con formato incorrecto {_cobro.Celular}");

                    _cobro.ResultadoEnvio = "8";
                    listaActualizacionWeb.Add(_cobro);
                    EscribirLinea();
                    return;
                }
            }
            catch (Exception ex)
            {
                Bitacora.Logger.Info($"Error al validar celular {ex.Message}");

                _cobro.ResultadoEnvio = "999";
                listaActualizacionWeb.Add(_cobro);
                EscribirLinea();
                return;
            }

            //VALIDAR MENSAJE
            try
            {
                mensaje = ConstruirMensaje(_cobro);

                if (mensaje == "")
                {
                    Bitacora.Logger.Info($"Error: No se pudo construir el mensaje");

                    _cobro.ResultadoEnvio = "7";
                    listaActualizacionWeb.Add(_cobro);
                    EscribirLinea();
                    return;
                }
            }
            catch (Exception ex)
            {
                Bitacora.Logger.Info($"Error al construir mensaje {ex.Message}");

                _cobro.ResultadoEnvio = "999";
                listaActualizacionWeb.Add(_cobro);
                EscribirLinea();
                return;
            }

            //ENVIAR MENSAJE
            try
            {
                //Nota: si se agota el saldo del servicio se interrumpe el envío
                EnviarSMSconWS(celular, mensaje, _cobro);
                listaActualizacionWeb.Add(_cobro);
                Bitacora.Logger.Info($"Se añade cobro a lista de actualizacion con estatus {_cobro.ResultadoEnvio}");

                EscribirLinea();
                Thread.Sleep(Convert.ToInt32(segundos_sleep_envio_sms * 1000));
            }
            catch (Exception ex)
            {
                Bitacora.Logger.Info($"Error al enviar mensaje por API en cobro {ex.Message}");

                _cobro.ResultadoEnvio = "999";
                listaActualizacionWeb.Add(_cobro);

                EscribirLinea();
                Thread.Sleep(Convert.ToInt32(segundos_sleep_envio_sms * 1000));
            }
        }

        private void EscribirLinea()
        {
            Bitacora.Logger.Info(Environment.NewLine + "---------------------------------------------------------------------------------------------------");
        }

        /// <summary>
        /// Quita la información de los objetos utilizados
        /// </summary>
        private void limpiarAlmacenamiento()
        {
            Lista_errores.Clear();
            listaCobros.Clear();
            listaActualizacionWeb.Clear();
        }

        /// <summary>
        /// Verifica el número de celular, quita los prefjios 044 y 045
        /// </summary>
        /// <param name="celular">Cadena con el número de celular</param>
        /// <returns>Celular sin prefijos si es correcto, cadena vacia si es incorrecto</returns>
        private string ValidarCelular(string celular)
        {
            //Quita los prefijos 044 y 045 del numero de celular
            if (celular.Substring(0, 3) == "044" || celular.Substring(0, 3) == "045")
            {
                celular = celular.Substring(3);
            }

            if (celular.Length != 10)
            {
                celular = string.Empty;
            }

            return celular;
        }

        /// <summary>
        /// Construye el mensaje a enviar en base a la información de un cobro
        /// </summary>
        /// <param name="_cobro">Objeto de la clase Cobro</param>
        /// <returns>Una cadena con el mensaje a enviar</returns>
        private string ConstruirMensaje(Cobro _cobro)
        {
            string mensaje = string.Empty;

            mensaje = Plantilla_mensaje;
            //PABS: Hemos recibido su abono por $|Monto| con folio |Recibo| para el contrato |Contrato| con saldo $|Saldo-Monto| el día |FechaHoraRecibo|

            //Reemplazar |Parametro| con informacion del cobro
            //Parametros disponibles: |Contrato|, |Saldo|, |Recibo|, |Monto|, |Cliente|, |Celular|, |Cobrador|, |Fecha_hora_evento|, |Estatus_cobro|, |No_cobro|, |Saldo-Monto|
            if (mensaje.Contains("|Contrato|"))
            {
                mensaje = mensaje.Replace("|Contrato|", _cobro.Contrato);
            }

            if (mensaje.Contains("|Saldo|"))
            {
                mensaje = mensaje.Replace("|Saldo|", $"{ String.Format("{0:0,0.00}", _cobro.Saldo)}");
            }

            if (mensaje.Contains("|Recibo|"))
            {
                mensaje = mensaje.Replace("|Recibo|", _cobro.Recibo);
            }

            if (mensaje.Contains("|Monto|"))
            {
                mensaje = mensaje.Replace("|Monto|", $"{ String.Format("{0:0,0.00}", _cobro.Monto)}");
            }

            if (mensaje.Contains("|Cliente|"))
            {
                mensaje = mensaje.Replace("|Cliente|", _cobro.Cliente);
            }

            if (mensaje.Contains("|Celular|"))
            {
                mensaje = mensaje.Replace("|Celular|", _cobro.Celular);
            }

            if (mensaje.Contains("|Cobrador|"))
            {
                mensaje = mensaje.Replace("|Cobrador|", _cobro.Cobrador);
            }

            if (mensaje.Contains("|Fecha_hora_evento|"))
            {
                mensaje = mensaje.Replace("|Fecha_hora_evento|", $"{_cobro.Fecha_hora_evento.ToString("dd/MM/yyyy HH:mm")}");
            }

            if (mensaje.Contains("|Estatus_cobro|"))
            {
                mensaje = mensaje.Replace("|Estatus_cobro|", _cobro.Estatus_cobro.ToString());
            }

            if (mensaje.Contains("|No_cobro|"))
            {
                mensaje = mensaje.Replace("|No_cobro|", _cobro.No_cobro);
            }

            if (mensaje.Contains("|Saldo-Monto|"))
            {
                mensaje = mensaje.Replace("|Saldo-Monto|", $"{ String.Format("{0:0,0.00}", _cobro.Saldo - _cobro.Monto)}");
            }

            //Limitar mensaje a 160 caracteres
            if (mensaje.Length >= Cantidad_caracteres)
            {
                mensaje = mensaje.Substring(0, Cantidad_caracteres - 2);
            }

            return mensaje;
        }

        /// <summary>
        /// Envia el mensaje a traves de un WebService
        /// </summary>
        /// <param name="_telefono">El número a quien se enviará el mensaje</param>
        /// <param name="_mensaje">Cadena con el mensaje a enviar</param>
        /// <param name="_cobro">el objeto cobro</param>
        private bool EnviarSMSconWS(string _telefono, string _mensaje, Cobro _cobro)
        {
            try
            {
                var client = new RestClient("https://api.smsmasivos.com.mx/sms/send");
                var request = new RestRequest(Method.POST);
                request.AddHeader("content-type", "application/json");
                request.AddHeader("apikey", Apikey);
                //request.AddCookie("__cfduid", "dd0b849de0087cd854583849abd8abd6c1603214316");
                //request.AddCookie("connect.sid", "s%3AF0xjq31PNmXeVE0cNOGkEkC2kvnkzQkE.i7DUB9ejY9yXNDKxUFEhux%2BXRIl3WOrs8o6%2BzCKPX10");

                string cadenaEnvio = "{\n\t\"message\":\"" + _mensaje +
                    "\",\n\t\"numbers\":\"" + _telefono +
                    "\",\n\t\"country_code\":\"52\",\n\t\"sandbox\":\"" + Sandbox + "\"\n}";

                Bitacora.Logger.Info("Llamada al servicio:" + Environment.NewLine + cadenaEnvio);

                request.AddParameter("application/json", cadenaEnvio, ParameterType.RequestBody);

                IRestResponse response = client.Execute(request);

                if (response.Content.Contains(@"sms_11"))
                {
                    try
                    {
                        _cobro.ResultadoEnvio = "3";
                        _cobro.DescripciónResultado = "Mensaje enviado";

                        Bitacora.Logger.Info($"Mensaje enviado: {_mensaje}");

                        RespuestaExito respuesta = JsonConvert.DeserializeObject<RespuestaExito>(response.Content);
                        _cobro.referencia = respuesta.references[0].reference;

                        Bitacora.Logger.Info($"Referencia: {_cobro.referencia}");
                    }
                    catch (Exception ex)
                    {
                        Bitacora.Logger.Info("Error al deserializar respuesta positiva: " + ex.ToString());
                    }

                    return true;
                }
                else
                {
                    string RespuestaEnvio = string.Empty;

                    //Manejar error al deserializar el objeto
                    try
                    {
                        RespuestaFallida respuesta = JsonConvert.DeserializeObject<RespuestaFallida>(response.Content);
                        RespuestaEnvio = respuesta.code;
                    }
                    catch (Exception ex)
                    {
                        //Bitacora.Logger.Fatal(ex, "Error al deserializar respuesta negativa");
                        //Bitacora.Logger.Info($"Error al deserializar respuesta negativa: " + ex.ToString());
                        Bitacora.Logger.Info($"Respuesta recibida: " + Environment.NewLine + response.Content);

                        //Cuando falla el servidor envia una respuesta html
                        if (response.Content.Contains("server"))
                        {
                            RespuestaEnvio = "server";
                        }

                        //Cuando falle, iniciar búsqueda en el JSON
                        if (response.Content.Contains("auth_01"))
                        {
                            RespuestaEnvio = "auth_01";
                        }
                        if (response.Content.Contains("sms_01"))
                        {
                            RespuestaEnvio = "sms_01";
                        }
                        if (response.Content.Contains("sms_02"))
                        {
                            RespuestaEnvio = "sms_02";
                        }
                        if (response.Content.Contains("sms_03"))
                        {
                            RespuestaEnvio = "sms_03";
                        }
                        if (response.Content.Contains("sms_04"))
                        {
                            RespuestaEnvio = "sms_04";
                        }
                        if (response.Content.Contains("sms_05"))
                        {
                            RespuestaEnvio = "sms_05";
                        }
                        if (response.Content.Contains("sms_06"))
                        {
                            RespuestaEnvio = "sms_06";
                        }
                        if (response.Content.Contains("sms_07"))
                        {
                            RespuestaEnvio = "sms_07";
                        }
                        if (response.Content.Contains("sms_12"))
                        {
                            RespuestaEnvio = "sms_12";
                        }
                        if (response.Content.Contains("sms_13"))
                        {
                            RespuestaEnvio = "sms_13";
                        }
                        if (response.Content.Contains("sms_15"))
                        {
                            RespuestaEnvio = "sms_15";
                        }
                        if (response.Content.Contains("sms_16"))
                        {
                            RespuestaEnvio = "sms_16";
                        }
                        if (response.Content.Contains("sms_17"))
                        {
                            RespuestaEnvio = "sms_17";
                        }
                        if (response.Content.Contains("sms_18"))
                        {
                            RespuestaEnvio = "sms_18";
                        }
                    }

                    //Asignar resultado del envio al cobro
                    switch (RespuestaEnvio)
                    {
                        case "auth_01":
                            _cobro.ResultadoEnvio = "-3";
                            _cobro.DescripciónResultado = "Usuario no autorizado";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        case "sms_01":
                            _cobro.ResultadoEnvio = "7";
                            _cobro.DescripciónResultado = "Mensaje indefinido";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        case "sms_02":
                            _cobro.ResultadoEnvio = "7";
                            _cobro.DescripciónResultado = "Mensaje muy largo";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        case "sms_03":
                            _cobro.ResultadoEnvio = "8";
                            _cobro.DescripciónResultado = "Número indefinido";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        case "sms_04":
                            _cobro.ResultadoEnvio = "8";
                            _cobro.DescripciónResultado = "Número con formato incorrecto";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        case "sms_05":
                            _cobro.ResultadoEnvio = "8";
                            _cobro.DescripciónResultado = "Código de país indefinido";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        case "sms_06":
                            _cobro.ResultadoEnvio = "7";
                            _cobro.DescripciónResultado = "Nombre muy largo";
                            _cobro.referencia = RespuestaEnvio;
                            break;

                        case "sms_07": //Si se agotó el saldo dejar de enviar mensajes

                            ContinuarEnviandoMensajes = false;
                            Bitacora.Logger.Info("Se agotó el saldo");
                            Bitacora.Logger.Fatal("Se agotó el saldo");
                            _cobro.ResultadoEnvio = "101";
                            _cobro.DescripciónResultado = "Se agotó el saldo";
                            _cobro.referencia = "sms_07";
                            break;

                        case "sms_12":
                            _cobro.ResultadoEnvio = "9";
                            _cobro.DescripciónResultado = "Error al enviar los mensajes";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        case "sms_13":
                            _cobro.ResultadoEnvio = "9";
                            _cobro.DescripciónResultado = "Error al enviar los mensajes";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        case "sms_15":
                            _cobro.ResultadoEnvio = "-201";
                            _cobro.DescripciónResultado = "Máximo 500 mensajes por envío";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        case "sms_16":
                            _cobro.ResultadoEnvio = "-201";
                            _cobro.DescripciónResultado = "Número repetido";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        case "sms_17":
                            _cobro.ResultadoEnvio = "-201";
                            _cobro.DescripciónResultado = "Máximo 1,000 envíos por día en modo de pruebas";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        case "sms_18":
                            _cobro.ResultadoEnvio = "6";
                            _cobro.DescripciónResultado = "Código de país no soportado";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        case "server":
                            _cobro.ResultadoEnvio = "0";
                            _cobro.DescripciónResultado = "Error del servidor. Se reintentará después.";
                            _cobro.referencia = RespuestaEnvio;
                            break;
                        default:
                            _cobro.ResultadoEnvio = "5";
                            _cobro.DescripciónResultado = "No hay descripción disponible para el error";
                            _cobro.referencia = $"Código de error desconocido: {response.Content}";
                            break;
                    }

                    Bitacora.Logger.Info($"Mensaje no enviado: {_cobro.DescripciónResultado} - {_cobro.ResultadoEnvio} - {_cobro.referencia}");

                    return false;
                }
            }
            catch (Exception ex)
            {
                ContinuarEnviandoMensajes = false;
                Bitacora.Logger.Info("Falla en método EnviarSMSconWS. Se detiene envio de mensajes ", ex.Message);
                Bitacora.Logger.Fatal(ex, "Falla en método EnviarSMSconWS ");

                _cobro.ResultadoEnvio = "999";
                _cobro.DescripciónResultado = "Error en método EnviarSMSconWS: " + ex.Message;

                return false;
            }
        }


        #endregion

        #region Actualización de resultados

        /// <summary>
        /// Revisa la lista de cobros por actualizar y las envia a actualizar
        /// </summary>
        private void RevisarListaActualizacion()
        {
            Cobro _cobro;

            Bitacora.Logger.Error("Comienza revisión de lista");

            do
            {
                if (listaActualizacionWeb.Count > 0)
                {
                    try
                    {
                        _cobro = listaActualizacionWeb[0];

                        if (ActualizarCobro(_cobro))
                        {
                            try
                            {
                                listaActualizacionWeb.RemoveAt(0);
                            }
                            catch (Exception ex)
                            {
                                Bitacora.Logger.Error($"{_cobro.Indice}. {_cobro.Recibo} -> Error al remover en lista de actualización el cobro {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Bitacora.Logger.Error($"Error al seleccionar cobro de lista de actualización: {ex.Message}");
                    }
                }

                //Espera cierta cantidad de segundos antes de seleccionar el siguiente elemento
                Thread.Sleep(Convert.ToInt32(Segundos_sleep_actualizacion_ecobro * 1000));

            } while (ContinuarActualizandoEcobro || listaActualizacionWeb.Count > 0);
                
            Bitacora.Logger.Error("Termina revisión de lista");
        }

        /// <summary>
        /// Identifica que al cobro X ya se le envió un mensaje, la identificacion puede ser un codigo de exito o de error
        /// </summary>
        /// <param name="folio">Folio del cobro</param>
        /// <param name="no_cobro">Id del cobro</param>
        /// <param name="idResultado">Id resultante del envio del mensaje</param>
        /// <param name="msgResultado">Mensaje resultante del envio del mensaje</param>
        private bool ActualizarCobro(Cobro _cobro)
        {
            //TEST
            //return true;
            //FIN TEST

            try
            {
                //Consumo de WS -> Actualizar cobro
                var client = new RestClient(WSActualizarCobro);
                var request = new RestRequest(Method.POST);
                request.AddHeader("content-type", "application/json");
                request.AddParameter("application/json", "{\n\"no_cobro\": " + _cobro.No_cobro +",\n\"sms_enviado\": \"" + _cobro.ResultadoEnvio + "\"\n}", ParameterType.RequestBody);
                IRestResponse response = client.Execute(request);

                if (response.Content.Contains("Cobro actualizado"))
                {
                    Bitacora.Logger.Error($"{_cobro.Indice}. {_cobro.Recibo} -> Actualizado -> {_cobro.ResultadoEnvio} - {_cobro.DescripciónResultado}");
                    return true;
                }
                else
                {
                    Bitacora.Logger.Error($"{_cobro.Indice}. {_cobro.Recibo} -> Fallido");
                    Bitacora.Logger.Fatal($"{_cobro.Indice}. {_cobro.Recibo} -> No se pudo actualizar el estatus a eCobro: " + response.Content);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Bitacora.Logger.Error($"{_cobro.Indice}. {_cobro.Recibo} -> Error en método ActualizarCobro {ex.ToString()}");
                Bitacora.Logger.Fatal(ex, $"{_cobro.Indice}. {_cobro.Recibo} -> Error en método ActualizarCobro");

                return false;
            }            
        }

        #endregion
    }
}
