using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

namespace WindowsFormsApp2
{
    // Authors: [Patrick Dale] - Primary Developer
    // Assistance: Grok (xAI) + Chatgpt - Technical Guidance and Debugging
    public partial class Form1 : Form
    {
        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string folderPath = folderDialog.SelectedPath;

                    string[] icoFiles = Directory.GetFiles(folderPath, "*.ico");
                    if (icoFiles.Length == 0)
                    {
                        MessageBox.Show("No .ico file found in this folder.");
                        return;
                    }

                    string iconPath = icoFiles[0]; // Full path, e.g., F:\Sort\Sweden\...\f36648400.ico
                    string iconFileName = Path.GetFileName(iconPath); // Just f36648400.ico
                    string iniPath = Path.Combine(folderPath, "desktop.ini");
                    string newline = Environment.NewLine;

                    try
                    {
                        // Step 1: Write desktop.ini with full path (80 bytes + 16 padding = 96 bytes)
                        string fullPathContent = $"[.ShellClassInfo]{newline}IconResource={iconPath},0{newline}";
                        byte[] fullPathBytes = Encoding.Default.GetBytes(fullPathContent); // ~80 bytes
                        byte[] paddedBytes = new byte[96];
                        Array.Copy(fullPathBytes, 0, paddedBytes, 0, fullPathBytes.Length);
                        File.WriteAllBytes(iniPath, paddedBytes);

                        // Step 2: Set folder to Read-only (mimicking DOpus)
                        File.SetAttributes(folderPath, File.GetAttributes(folderPath) | FileAttributes.ReadOnly);

                        // Step 3: Set desktop.ini to Hidden and System
                        File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);

                        // Step 4: Notify the shell for immediate Explorer and DOpus refresh
                        IntPtr folderPtr = Marshal.StringToHGlobalUni(folderPath);
                        SHChangeNotify(0x08000000, 0x1000, folderPtr, IntPtr.Zero); // SHCNE_UPDATEDIR, SHCNF_PATHW
                        Marshal.FreeHGlobal(folderPtr);

                        // Step 5: Wait and prepare for second write
                        Thread.Sleep(500); // 500ms delay to release locks
                        File.SetAttributes(iniPath, FileAttributes.Normal); // Ensure writable

                        // Step 6: Rewrite with folder-relative path (no leading \)
                        string relativePathContent = $"[.ShellClassInfo]{newline}IconResource={iconFileName},0{newline}";
                        byte[] relativePathBytes = Encoding.Default.GetBytes(relativePathContent); // ~50 bytes
                        byte[] finalPaddedBytes = new byte[96];
                        Array.Copy(relativePathBytes, 0, finalPaddedBytes, 0, relativePathBytes.Length);
                        File.WriteAllBytes(iniPath, finalPaddedBytes);

                        // Step 7: Reapply Hidden and System attributes
                        File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);

                        // Step 8: Notify the shell again
                        folderPtr = Marshal.StringToHGlobalUni(folderPath);
                        SHChangeNotify(0x08000000, 0x1000, folderPtr, IntPtr.Zero);
                        Marshal.FreeHGlobal(folderPtr);

                        // Step 9: Force DOpus refresh via scripting
                        string dopusrtPath = @"C:\Program Files\GPSoftware\Directory Opus\dopusrt.exe";
                        ProcessStartInfo processInfo = new ProcessStartInfo
                        {
                            FileName = dopusrtPath,
                            Arguments = $"/cmd RunScript seticon.vbs \"{folderPath}\" \"{iconFileName}\"",
                            UseShellExecute = true,
                            Verb = "runas" // Request elevation
                        };
                        Process.Start(processInfo).WaitForExit();

                        MessageBox.Show("Icon successfully applied and set to relative path for portability!");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        MessageBox.Show($"Permission error at: {ex.Message}\nTry running as administrator or check folder permissions.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Unexpected error: {ex.Message}\nStack: {ex.StackTrace}");
                    }
                }
            }
        }
    }
}
