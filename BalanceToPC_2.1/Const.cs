using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BalanceToPC_2._1
{
    class Const
    {
        // For BalanceConfigFile.txt file variables
        public const int PortName = 0;
        public const int CupWeight = 1;
        public const int AutoENTER = 2;
        public const int Space = 0x20;
        public const int LinesNumb = 3;
        public const int PortNLine = 2;
        public const int CupWLine = 5;

        // For serial port stuff
        public const int BalanceMsgLenght = 18;
        public const int CR = 16;
        public const int LF = 17;
        public const int CarrReturn = 0x0D;
        public const int NewLine = 0x0A;

        public const int PortFound = 101;
        public const int Connected = 102;
        public const int AccessDenied = 103;

        // Text Files names
        public const string LogFile = "LogFile.txt";
        public const string ConfigFile = "ConfigFile.txt";

        // Error - notification list
        public const int NotFoundParamFile = 101;
        public const int NoPorts = 102;
        public const int NoBalancePort = 103;
        public const int NoCurrProc = 104;
        public const int IncorrDataPack = 105;

        public const int OK = 125;
    }
}
