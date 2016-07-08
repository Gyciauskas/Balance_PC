using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BalanceToPC_2._1
{
    class Logger
    {
        private string m_exePath = string.Empty;

       /* public Logger(string logMessage)
        {
            Write(logMessage);
        }*/

        public void Write(string logMessage)
        {
            m_exePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            try
            {
                // Net jei failo ir neranda, tai ji sukuria. Taip garantuojamas Log irasymas
                using (StreamWriter write = File.AppendText(m_exePath + "\\" + Const.LogFile))
                {
                    Log(logMessage, write);
                }
            }
            catch (Exception)
            {
            }
        }

        public void Log(string logMessage, TextWriter txtWriter)
        {
            try
            {
                txtWriter.Write("\r\n{0} ",
                    DateTime.Now.ToShortTimeString());
                txtWriter.WriteLine("Log: {0}", logMessage);
            }
            catch (Exception) { }
        }
    }
}
