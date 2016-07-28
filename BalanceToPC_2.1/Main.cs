using System;
using System.IO.Ports;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace BalanceToPC_2._1
{
    public partial class Form1 : Form
    {
        // Sekti svarstyklių duomenų būseną programoje
        enum RxState {
            Start,
            Waiting,
            DataReceived };

        // Sekti programos būseną 
        enum PortState {
            CheckFiles,
            WaitParam,
            Start,
            NoPortAvail,
            SearchMatching,
            Connecting,
            PortAccessDenied,
            Connected };

        // Parametrai, gaunami iš nustatymų failo
        struct Parameters {
            public string Port;
            public float CupWeight;
            public bool AutoENTER;
        };

        // Globalūs kintamieji
        NotifyIcon BalanceIcon;
        SerialPort BalanceSPort;
        PortState CurrPortState;
        RxState DataReceiv_st;
        Logger Log;
        Parameters BalanceParam;

        byte[] _RxData = new byte[Const.BalanceMsgLenght];
        string _exePath;
        bool _ProgramExit = false;
        bool _RestartProgramStates = false;

        public Form1()
        {
            InitializeComponent();

            System.Drawing.Icon Scales;

            #region Form stuff
            Scales = new System.Drawing.Icon("_scale.ico");

            // Sukurti įkoną, priskirti svarstyklių logotipą
            BalanceIcon = new NotifyIcon();
            BalanceIcon.Icon = Scales;
            BalanceIcon.Visible = true;

            System.Windows.Forms.MenuItem ProgNameItem = new System.Windows.Forms.MenuItem("Apie \"BalancePC\"");
            System.Windows.Forms.MenuItem QuitMenuItem = new System.Windows.Forms.MenuItem("Uždaryti programą");
            ContextMenu ContxMenu = new ContextMenu();

            ContxMenu.MenuItems.Add(ProgNameItem);
            ContxMenu.MenuItems.Add(QuitMenuItem);
            BalanceIcon.ContextMenu = ContxMenu;

            QuitMenuItem.Click += QuitMenuItem_Click;
            ProgNameItem.Click += ProgNameItem_Click;

            // Paslėpti WinForm'ą, mums ji nereikalinga
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            BalanceIcon.MouseClick += new MouseEventHandler(PerformanceIcon_MouseClick);
            #endregion

            // konfiguruoti thread'ą
            Thread MainThread = new Thread(new ThreadStart(MainProgramThread));

            // By default, thread is foreground. Change to background (background thread'as išsijungia kartu su application.exit())
            MainThread.IsBackground = true;
            MainThread.Start();

            //Inicializuoti state'us
            CurrPortState = PortState.CheckFiles;
            DataReceiv_st = RxState.Start;
        }

        private void MainProgramThread()
        {
            int Func_answer;

            // Priskiriamos pradinės reikšmės struktūrai
            BalanceParam.Port = "";
            BalanceParam.CupWeight = 0.0f;

            // Pradėti logą
            Log = new Logger();

            while (true)
            {
                switch (CurrPortState)
                {
                    case PortState.CheckFiles:
                        try
                        {
                            Func_answer = GetProgramParamValues();

                            if (Func_answer == Const.NotFoundParamFile)
                                // Nustatymų failas nerastas, informuoti vartotoją bei išjungti programą
                                Inform_ShuttDownPrgm("Parametrų failas nerastas: " + Const.ConfigFile);
                            else
                                CurrPortState = PortState.Start;
                        }
                        catch (ArgumentException ex)
                        {
                            // Jeigu neranda nustatymų faile, tuomet pereiti i WaitParam state ir laukti kol juos gaus iš nustatymų lango.
                            if (ex.ParamName.Equals("Empty COM port"))
                            {
                                MessageBoxCreator("Neįrašytas COM portas nustatymuose.", "Klaida", MessageBoxIcon.Warning);
                                CurrPortState = PortState.WaitParam;
                            }
                            else if (ex.ParamName.Equals("Incorrect cup weight") || ex.ParamName.Equals("Empty cup weight"))
                            {
                                MessageBoxCreator("Neįrašytas arba netinkamai įrašytas taurelės svoris.", "Klaida", MessageBoxIcon.Warning);
                                CurrPortState = PortState.WaitParam;
                                _RestartProgramStates = true;
                            }
                            else
                                Inform_ShuttDownPrgm(ex.Message);
                        }
                        catch (Exception ex)
                        {
                            Inform_ShuttDownPrgm(ex.Message);
                        }
                        break;

                    // Laukti iki kol vartotojas įrašys COM portą/neteisingą taurelės svorį nustatymuose.
                    case PortState.WaitParam:
                        break;

                    case PortState.Start:
                        Func_answer = SearchingPort(BalanceParam.Port);

                        if (Func_answer == Const.PortFound)
                        {
                            CurrPortState = PortState.Connecting;
                            Log.Write("Connecting");
                        }
                        else
                        {
                            if (Func_answer == Const.NoPorts) Log.Write("No ports found");

                            else if (Func_answer == Const.NoBalancePort) Log.Write("Balance port not found");

                            CurrPortState = PortState.SearchMatching;
                        }
                        break;

                    case PortState.SearchMatching:
                        Func_answer = SearchingPort(BalanceParam.Port);

                        if (Func_answer == Const.PortFound)
                        {
                            CurrPortState = PortState.Connecting;
                            Log.Write("Connecting");
                        }
                        break;

                    case PortState.Connecting:
                        try
                        {
                            Func_answer = ConnectToPort(BalanceParam.Port);

                            if (Func_answer == Const.Connected)
                            {
                                CurrPortState = PortState.Connected;
                                DataReceiv_st = RxState.Waiting;
                                Log.Write("Connected");
                            }
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            CurrPortState = PortState.PortAccessDenied;
                            Log.Write(ex.Message);
                        }
                        catch (Exception ex)
                        {
                            Inform_ShuttDownPrgm(ex.Message);
                        }
                        break;

                    case PortState.PortAccessDenied:
                        try
                        {
                            // Mėginti iš naujo jungtis į COM portą
                            Func_answer = ConnectToPort(BalanceParam.Port);

                            if (Func_answer == Const.Connected)
                            {
                                CurrPortState = PortState.Connected;
                                DataReceiv_st = RxState.Waiting;
                                Log.Write("Connected");
                            }
                        }
                        // Ignoruooti šitą klaidą, nes tai reiškia jog šiuo metu COM portas užimtas
                        catch (UnauthorizedAccessException)
                        { }
                        catch (Exception ex)
                        {
                            Inform_ShuttDownPrgm(ex.Message);
                        }
                        break;

                    case PortState.Connected:
                        if (DataReceiv_st == RxState.DataReceived)
                        {
                            try
                            {
                                Func_answer = WriteMeasurement(BalanceParam);

                                // Šiuo atveju tik įrašome į log'ą.
                                if (Func_answer == Const.IncorrDataPack)
                                    Log.Write("Incorrect data pack!");
                            }
                            catch (ArgumentOutOfRangeException ex)
                            {
                                if (ex.ParamName == "Weight out range")
                                    MessageBoxCreator("Svoris neatitinka nustatytų ribų (0.0 - 120.0g).", "Įspėjimas!", MessageBoxIcon.Warning);
                                else
                                    Inform_ShuttDownPrgm(ex.Message);
                            }
                            catch (Exception ex)
                            {
                                Inform_ShuttDownPrgm(ex.Message);
                            }
                            finally
                            {
                                DataReceiv_st = RxState.Waiting;
                            }
                        }
                        break;
                }
                // Miegoti 1s
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        ///     Paspaudus svartyklių ikoną kairiu pelės klavišu, iššoks WinForm langas su nustatymais.
        ///     Tačiau prieš atidarant langą, reikia atnaujinti visus esamus COM portus, taurelės svorį, bei AutoENTER būseną.
        /// </summary>
        private void PerformanceIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (FormWindowState.Minimized == this.WindowState)
                {
                    this.Show();
                    this.WindowState = FormWindowState.Normal;

                    // Pirma, ištriname buvusias reikšmes
                    ComPortsList.Items.Clear();
                    CupWeight.Text = String.Empty;

                    // Surašome naujas reikšmes
                    string[] ports = SerialPort.GetPortNames();

                    if (ports != null || ports.Length != 0)
                    {
                        ComPortsList.Items.AddRange(ports);
                        ComPortsList.SelectedItem = BalanceParam.Port;
                    }

                    CupWeight.AppendText(BalanceParam.CupWeight.ToString());

                    if (BalanceParam.AutoENTER)
                        AutoEnterBox.Checked = true;
                    else
                        AutoEnterBox.Checked = false;
                }
                else if (FormWindowState.Normal == this.WindowState)
                {
                    this.WindowState = FormWindowState.Minimized;
                    this.ShowInTaskbar = false;
                    this.Hide();
                }
            }
        }

        /// <summary>
        ///     Informacija apie programą ir autoriaus vardas pavardė
        /// </summary>
        private void ProgNameItem_Click(object sender, EventArgs e)
        {
            MessageBoxCreator("Programa parašyta: Gytis Petrusevičius \nBETA versija v2.0.1", "Apie \"BalancePC\"", MessageBoxIcon.Information);
        }

        /// <summary>
        ///     Programos uždarymas. Pirma uždaromas COM portas, vėliau ir pati programa.
        /// </summary>
        private void QuitMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Jei programa būtų prijungta prie įšorinio virtualaus COM porto
                // Ir prieš išjungiant programą būtų pirma ištrauktas laidas, uždarymo atveju išmestu klaidą. 
                // Tam reikalingas try catch blokas.
                BalanceSPort.Close();
            }
            catch (Exception) { }

            // Jeigu ikonos matomumo prieš išjungiant programą nenustatai false, ikona išlieka iki kol neprabrauki pele
            BalanceIcon.Visible = false;
            _ProgramExit = true;

            System.Windows.Forms.Application.Exit();
        }

        /// <summary>
        ///     Šis metodas sugeneruoja MessageBox langą vartotojui su gauta informacija.
        /// </summary>
        private void MessageBoxCreator(string Message, string Title, MessageBoxIcon messageIcon)
        {
            MessageBox.Show(Message, Title, MessageBoxButtons.OK, messageIcon);
        }

        /// <summary>
        ///     Nusiųsti MessageBox vartotojui su klaidos aprašymu ir uždaryti programą.
        /// </summary>
        /// <param name="message">Klaidos aprašymas</param>
        private void Inform_ShuttDownPrgm(string message)
        {
            // Informacija vartotojui apie klaidą
            MessageBoxCreator(message + "\nPrograma bus uždaryta.", "Klaida!", MessageBoxIcon.Error);

            // Uždaryti programą
            BalanceIcon.Visible = false;
            _ProgramExit = true;

            System.Windows.Forms.Application.Exit();
        }

        /// <summary>
        ///     Čia bus išsaugoti vartotojo pakeisti parametrai.
        ///     Tai COM portas ir/arba taurelės svoris.
        /// </summary>
        private void SaveChanges_Click(object sender, EventArgs e)
        {
            StreamWriter ConfigFile = null;
            float fcupWeight;
            bool NewComPort = false;
            bool AutoENTER = false;

            // Gauti nustatymuose įrašytas reikšmes
            string SelectComPort = (string)ComPortsList.SelectedItem;
            string cupWeight = CupWeight.Text;

            // Patikrinti ar reikšmė nėra tuščia/lygi null.
            if (String.IsNullOrEmpty(SelectComPort))
            {
                MessageBoxCreator("Nepasirinktas COM portas", "Įspėjimas", MessageBoxIcon.Information);
                return;
            }

            // Patikrinti ar reikšmė nėra tuščia/lygi null.
            if (String.IsNullOrEmpty(cupWeight))
            {
                MessageBoxCreator("Neįrašyta taurelės reišmė", "Įspėjimas", MessageBoxIcon.Information);
                return;
            }
            else
            {
                // Reikšmė įrašyta, patikrinti ar ji konvertuojasi į float kintamojo tipą.
                if (!float.TryParse(cupWeight, out fcupWeight))
                {
                    MessageBoxCreator("Netinkamai irasytas taureles svoris", "Įspėjimas", MessageBoxIcon.Information);
                    return;
                }
            }

            // Patikrinti AutoENTER reikšmę
            if (AutoEnterBox.Checked)
                AutoENTER = true;
            else
                AutoENTER = false;

            try
            {
                using (ConfigFile = new StreamWriter(_exePath + "\\" + Const.ConfigFile))
                {
                    // Patikrinti, ar pasikeitė COM portas. Jeigu taip, irašom naują ir perkraunam programos state'us
                    if (!SelectComPort.Equals(BalanceParam.Port))
                        NewComPort = true;

                    // Įrašyti būtina betkokiu atveju, kad peršokčiau į kitą eilutę.
                    ConfigFile.WriteLine(SelectComPort);

                    if (!cupWeight.Equals(BalanceParam.CupWeight))
                        BalanceParam.CupWeight = float.Parse(cupWeight);

                    // Įrašyti būtina betkokiu atveju, kad peršokčiau į kitą eilutę.
                    ConfigFile.WriteLine(cupWeight);

                    if(!AutoENTER.Equals(BalanceParam.AutoENTER))
                    {
                        ConfigFile.WriteLine(AutoENTER);
                        BalanceParam.AutoENTER = AutoENTER;
                    }
                }

                // Irašyta sėkmingai, galima minimalizuoti langą.
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Hide();

                // Jeigu naujas portas, perkrauti status
                // Jeigu ir vis delto taip ivyko, kad programai pasileidus portas buvo irasytas o taureles svoris ne
                // reikia irgi restartuoti programos state'us.
                if (NewComPort || _RestartProgramStates)
                {
                    try
                    {
                        // Close balance serial port, before shutt down. It could throw exception if before shutt down program, you throw cable.
                        BalanceSPort.Close();
                    }
                    catch (Exception) { }

                    // Programos state'us pakeisti į pradinius
                    CurrPortState = PortState.CheckFiles;
                    DataReceiv_st = RxState.Start;
                }
            }
            catch (Exception ex)
            {
                MessageBoxCreator(ex.Message, "Klaida!", MessageBoxIcon.Error);
            }
        }

        /// <summary>
        ///     Preliminaliai patikrinti ar duomenys neklaidinti. Įdealu būtų CRC, tačiau svartyklės jo negeneruoja.
        ///     Konvertuoti duomenys į float kintamąjį.
        ///     Išsiųsti duomenys kur vartotojas nori.
        /// </summary>
        /// <param name="BalanceParam">Perkeliama struktūra, su įrašytais nustatymais</param>
        /// <returns>Grąžina statusą int kintamuoju arba išmeta klaidą</returns>
        private int WriteMeasurement(Parameters BalanceParam)
        {
            // Jei duomenų paketo paskutiniai byte yra CR ir LF, galima teigti, kad gautas duomenų paketas - OK.
            if (_RxData[Const.CR] == Const.CarrReturn && _RxData[Const.LF] == Const.NewLine)
            {
                float fWeight;

                fWeight = ConvertMeasurement(BalanceParam.CupWeight);

                if (fWeight >= 120.0f || fWeight <= 0.00f)
                    throw new ArgumentOutOfRangeException("Weight out range");

                SendKeys.SendWait(fWeight.ToString());

                // Tik jei AutoENTER true
                if(BalanceParam.AutoENTER)
                    SendKeys.SendWait("{ENTER}");

                return Const.OK;
            }
            else {
                return Const.IncorrDataPack;
            }
        }

        /// <summary>
        ///     Iėškoma ar yra kompiuteryje veikiančių COM portų, jeigu randama,
        ///     tikrinama ar vienas jų atitinka nustatymuose įrašytui portui
        /// </summary>
        /// <param name="PortName">Iš "ConfigFile.txt" failo gautas COM porto pavadinimas</param>
        /// <returns>Vieną iš 3 galimų reikšmių int kintamojo pavidalu: Nerasta portų, portas (atitinkantis svarstyklių portą) rastas arba nerastas</returns>
        public int SearchingPort(string PortName)
        {
            // Gauti visus COM portus esančius kompiuteryje
            string[] ports = SerialPort.GetPortNames();

            if (ports == null || ports.Length == 0)
                return Const.NoPorts;

            // Bent vienas portas rastas
            else
            {
                // Patikrinti ar kuris sutampa su svarstyklių COM portu
                foreach (string port in ports)
                {
                    // OrdinalIgnoreCase - "com", "Com", and "COM" būtų priskirti kaip lygus vienas kitam.
                    if (port.Equals(PortName, StringComparison.OrdinalIgnoreCase))
                        return Const.PortFound;
                }

                // Svarstykliu portas nerastas
                return Const.NoBalancePort;
            }
        }

        /// <summary>
        ///     Inicializuoti parametrus svarstyklių COM portui bei prisijungti prie svarstyklių porto.
        /// </summary>
        /// <param name="PortName">Svarstyklių COM porto pavadinimas</param>
        /// <returns>Prisijungimo atveju statusą jog prisijungta, klaidos atveju išmes klaidą</returns>
        private int ConnectToPort(string PortName)
        {
            // Sutas reikalingas tam atvejui, jeigu patektum i AccessDenied state ir kiekviena kartą bandant jungtis,
            //nereikėtu iš naujo konfiguruoti Serial porto
            if (CurrPortState == PortState.Connecting)
            {
                BalanceSPort = new SerialPort(PortName, 9600, Parity.None, 7, StopBits.One);

                BalanceSPort.Handshake = Handshake.None;
                BalanceSPort.RtsEnable = true;
                BalanceSPort.ReceivedBytesThreshold = Const.BalanceMsgLenght;

                // Enable serial port interrupt
                BalanceSPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
            }

            BalanceSPort.Open();

            return Const.Connected;
        }

        /// <summary>
        ///     Gauto pilno duomenų paketo event'as.
        ///     
        ///     Svarstuklių duomenų paketo pavyzdys:
        ///     0     1     2     3     4     5     6     7     8     9     10    11    12    13    14    15    16    17   // Total 18 bytes!
        ///     0x2B, 0x20, 0x20, 0x20, 0x31, 0x38, 0x2E, 0x38, 0x38, 0x5B, 0x38, 0x5D, 0x20, 0x20, 0x67, 0x20, 0x0D, 0x0A
        ///     +     _     _     _     1     8     .     8     8     [     8     ]     _     _     g     _     CR    LF;
        /// </summary>
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            // Collect data only with proper state
            if (DataReceiv_st == RxState.Waiting)
            {
                SerialPort sp = (SerialPort)sender;

                // Write to byte array all received bytes
                for (int i = 0; i < Const.BalanceMsgLenght; i++)
                {
                    _RxData[i] = (byte)sp.ReadByte();
                }

                // Gavus paketą, pakeisti statusą 
                DataReceiv_st = RxState.DataReceived;
            }
        }

        /// <summary>
        ///     Konvertuoja gautus duomenis iš svartyklių (byte masyvą) į float kintamojo tipą ir jį grąžina.
        ///     Vykdomos aritmetikos paaiškinimas: kadangi byte reprezentuoti dec tipu, tai 0 lygūs 48. Todėl is gauto skaičiaus atimi 48 ir gauni tikrają reikšmę.
        /// </summary>
        /// <param name="CupWeight">Taurelės svoris, gautas iš "BalanceInitFile.txt" failo</param>
        /// <returns>Svarstyklių atsiųsta svorį, konvertuota į float kintamąjį</returns>
        private float ConvertMeasurement(float CupWeight)
        {
            float[] tempFloat = new float[6];

            // Balance, instead send zero, give space! So allways check!
            // In case, that if measurement is greater than 100g
            if (_RxData[3] == Const.Space)
                tempFloat[0] = 0.0f;
            else
                tempFloat[0] = ((float)_RxData[3] - 48) * 100;

            // In case, that greater than 10g
            if (_RxData[4] == Const.Space)
                tempFloat[1] = 0.0f;
            else
                tempFloat[1] = ((float)_RxData[4] - 48) * 10;           // 48-57 equal 0-9

            tempFloat[2] = _RxData[5] - 48;
            tempFloat[3] = ((float)_RxData[7] - 48) / 10;
            tempFloat[4] = ((float)_RxData[8] - 48) / 100;
            tempFloat[5] = ((float)_RxData[10] - 48) / 1000;

            // Tam, kad konvertuociau bytus i floata. reikia sudeti.
            float fWeight = tempFloat[0] + tempFloat[1] + tempFloat[2] + tempFloat[3] + tempFloat[4] + tempFloat[5];

            // Iš gauto matavimo, atimti reikia taurelės svorį.
            fWeight = fWeight - CupWeight;

            return fWeight;
        }

        /// <summary>
        ///     Norint išvengti klaidų, ribojami kokios reikšmes galima įrašyti i text box'ą. 
        ///     Galimi tik skaičiai ir vienas kablelis.
        /// </summary>
        private void CupWeight_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != ','))
                e.Handled = true;

            // Jei jau padėtas kablelis, nustatyti "Handled = true" atšaukti KeyPress event'ą
            if ((e.KeyChar == ',') && (sender as System.Windows.Forms.TextBox).Text.IndexOf(',') > -1)
                e.Handled = true;
        }

        /// <summary>
        ///     Atidaryti programos nustatymų failą, pasiimti faile esančias reikšmes.
        ///     Pirmas parametras COM portas;
        ///     Antras parametras taurelės svoris;
        ///     Trečias parametras automatinis ENTER paspaudimas;
        ///     Gautas reikšmes įrašyti į struktūrą, pirma patikrinus ar duomenys teisingi.
        ///     Klaidos atveju išmesti klaidą arba grąžinti reikšmę.
        /// </summary>
        private int GetProgramParamValues()
        {
            // Gauti path'ą, jis globalus kintamasis, visiems prieinamas
            _exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            //Delete all records from log file (jokiu patikrinimų nėra, nes jeigu net ir nebutu rastas failas, jis tokiu atveju būtų sukurtas iš naujo)
            File.WriteAllText(_exePath + "\\" + Const.LogFile, string.Empty);

            // Check if balance parameters file is available. If no, shut down program
            if (File.Exists(_exePath + "\\" + Const.ConfigFile))
            {

                using (StreamReader ParamFile = new StreamReader(_exePath + "\\" + Const.ConfigFile))
                {
                    for (int line = 0; line < Const.LinesNumb; line++)
                    {
                        string Param = ParamFile.ReadLine();

                        switch (line)
                        {
                            // COM port
                            case Const.PortName:
                                if (String.IsNullOrEmpty(Param))
                                    // reikia irasyti reiksmes, programa iki tol neveiks, lauks. Cia is esmes taip bus pirma karta programa ikelus i pc.
                                    throw new System.ArgumentNullException("Empty COM port", "Neįrašytas COM portas nustatymuose.");
                                else
                                    BalanceParam.Port = Param;
                                break;

                            // Cup weight
                            case Const.CupWeight:
                                if (String.IsNullOrEmpty(Param))
                                    throw new System.ArgumentNullException("Empty cup weight", "Neįrašytas taurelės svoris");
                                else
                                {
                                    // Prieš priskiriant reikšmę patikrinti ar gauta reikšmė yra skaičius, jei ne, išmesti klaidą.
                                    if (!float.TryParse(Param, out BalanceParam.CupWeight))
                                    {
                                        throw new System.ArgumentException("Netinkamai įrašytas taurelės svoris! Svoris nepriskirtas. Įrašykite parametruose.",
                                            "Incorrect cup weight");
                                    }

                                    BalanceParam.CupWeight = float.Parse(Param);
                                }
                                break;

                            case Const.AutoENTER:
                                // Klaidos atveju, AutoENTER priskirti false
                                if (String.IsNullOrEmpty(Param))
                                    BalanceParam.AutoENTER = false;
                                else
                                {
                                    if (!bool.TryParse(Param, out BalanceParam.AutoENTER))
                                        BalanceParam.AutoENTER = false;
                                    else
                                        BalanceParam.AutoENTER = bool.Parse(Param);
                                }
                                break;
                        }
                    }
                }
                return Const.OK;
            }
            else
                return Const.NotFoundParamFile;
        }

        /// <summary>
        ///     Būtina atskirti, ar tai bandoma nustatymų langą uždaryti ar norima programa išjungti.
        /// </summary>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Jeigu tik norima uždaryti nustatymų langą, atšaukti Winform išjungimą priskiriant e.cancel = true
            if (!_ProgramExit)
                e.Cancel = true;

            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();
        }
    }
}
