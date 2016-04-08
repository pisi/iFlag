using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using System.Xml;
using System.Threading;
using System.ComponentModel;

namespace iFlag
{
    public partial class mainForm : Form
    {

                                                  // Stores current software updates level of involvement
        string updatesLevel = Settings.Default.Updates;
        string updateVersion = null;              // Version string of the update (if detected)
        string updateDownloadURL = "";            // URL to the update download
        string updateChanges = "";                // Copy of the changelog

        string version = major + "." + minor;     // Current app version as a string

        private Thread updateSoftwareThread;      // To not hold up the startup, check for updates
                                                  // is done in a separate thread

        private void startUpdater()
        {
            updateSoftware();
        }

                                                  // Checks if expected and actual firmware versions match,
                                                  // in which case it returns `true`.
        private bool deviceUpdated()
        {
            return firmwareVersionMajor == firmwareMajor
                && firmwareVersionMinor == firmwareMinor;
        }

                                                  // Checks if currently installed version match the latest
                                                  // update available on selected updates level.
                                                  // Returns `true` when software is up to date.
        private bool softwareUpdated()
        {
            return updateVersion == null || version == updateVersion;
        }

                                                  // Uses embedded `avrdude` tool to flash the device's memory
                                                  // with the firmware distributed along the software
        private void updateFirmware()
        {
            hardwareLight.BackColor = Color.FromName("Blue");
            commLabel.Text = "Programming with v" + firmwareMajor + "." + firmwareMinor + "...";

            SP.Close();
            deviceConnected = false;
            connectTimer.Enabled = false;
            demoTimer.Enabled = false;
            greeted = false; 
            tryPortIndex = 0;
                
            Process process = new Process();
            ProcessStartInfo info = new ProcessStartInfo();
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.FileName = "cmd.exe";
            info.Arguments = "/C device\\tools\\avrdude\\avrdude -Cdevice\\tools\\avrdude\\avrdude.conf -q -q -patmega328p -carduino -P" + port + " -b115200 -D -Uflash:w:device\\firmware.hex:i";
            process.StartInfo = info;
            process.Start();
            process.WaitForExit();
            Console.WriteLine(info.Arguments);
            Console.WriteLine(process.ExitCode);
        }

                                                  // Runs a separate thread, which will check for app updates
        private void updateSoftware()
        {
            updateSoftwareThread = new Thread(UpdateWorkerThread);
            updateSoftwareThread.Start();  
        }

                                                  // If iFlag doesn't find the hardware within 30seconds
                                                  // it will assume, that a brand new Arduino board is plugged in
                                                  // and will activate a otherwise invisible options menu item
                                                  // allowing the user to initialize the board. Last known port
                                                  // in the list is used in that cae
        private void initiationTimer_Tick(object sender, EventArgs e)
        {
            port = ports[ports.Length - 1];
            
            if (!deviceConnected && port != "COM1")
            {
                initiationTimer.Stop();
                initiateBoardMenuItem.Visible = true;
                initiateBoardMenuItem.Text += port;
            }
            else
            {
                startCommunication();
            }
        }

        private void initiateBoardMenuItem_Click(object sender, EventArgs e)
        {
            initiateBoardMenuItem.Visible = false;
            port = ports[ports.Length - 1];
            updateFirmware();
            connectTimer.Enabled = true;
        }

                                                  // Jumps on the internet to retreive a XML version file for
                                                  // selected updates level, reads the version information inside
                                                  // and stores these data into vars for later use.
                                                  // Returns `true` when there is an update.
        private bool CheckSoftwareVersion()
        {
            XmlTextReader reader;
            try
            {
                string xmlURL = updateURL + "." + updatesLevel + ".xml";
                reader = new XmlTextReader(xmlURL);
                reader.MoveToContent();
                string elementName = "";
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "iflag")
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                            elementName = reader.Name;
                        else
                        {
                            if (reader.NodeType == XmlNodeType.Text && reader.HasValue)
                            {
                                switch (elementName)
                                {
                                    case "version":
                                        updateVersion = reader.Value;
                                        break;
                                    case "url":
                                        updateDownloadURL = reader.Value;
                                        break;
                                    case "changelog":
                                        updateChanges = reader.Value;
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                //if (reader != null) reader.Close();
            }
            return !softwareUpdated();
        }

                                                  // Asynchronously handles the software update check
                                                  // and adjusts the main UI based on its findings
        private void UpdateWorkerThread()  
        {  
            if (CheckSoftwareVersion())
            {
                this.InvokeEx(f => f.updateLinkLabel.Show());
            }
            else
            {
                this.InvokeEx(f => f.updateLinkLabel.Hide());
            }
        }
    }
}

                                                  // This bit below is needed to overcome the thread lock-in
                                                  // and perform actions on the main Form in the main thread
public static class ISynchronizeInvokeExtensions
{
  public static void InvokeEx<T>(this T @this, Action<T> action) where T : ISynchronizeInvoke
  {
    if (@this.InvokeRequired)
    {
      @this.Invoke(action, new object[] { @this });
    }
    else
    {
      action(@this);
    }
  }
}