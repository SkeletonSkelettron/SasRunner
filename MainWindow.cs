using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Gtk;

public partial class MainWindow : Gtk.Window
{
    public MainWindow() : base(Gtk.WindowType.Toplevel)
    {
        Build();
    }

    protected void OnDeleteEvent(object sender, DeleteEventArgs a)
    {
        Application.Quit();
        a.RetVal = true;
    }

    protected void OnButton2Clicked(object sender, EventArgs e)
    {
        button2.Sensitive = false;
        if (combine_spectracheck.Active)
        {
            GtkScrolledWindow3.Visible = true;
            GtkScrolledWindow3.Activate();
        }
        if (string.IsNullOrEmpty(filechooserwidget1.CurrentFolder))
        {
            ShowDialog("Please choose working folder");
            button2.Sensitive = true;
        }
        else
        {
            ShowDialog("This program works only and if only setsas.sh script could be run from any place." +
                    "if it does not, then add it to the path.");
            Thread t = new Thread(new ThreadStart(() =>
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try
                {

                    List<string> copiedFiles = new List<string>();
                    System.Diagnostics.ProcessStartInfo psi
                            = new System.Diagnostics.ProcessStartInfo("find", "-type f -name \"*.tar.gz\"");
                    psi.RedirectStandardOutput = true;
                    psi.UseShellExecute = false;
                    psi.WorkingDirectory = filechooserwidget1.CurrentFolder;
                    System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
                    p.WaitForExit();
                    string tool_output = p.StandardOutput.ReadToEnd();

                    /*psi = new System.Diagnostics.ProcessStartInfo("cp", "s f");
                    psi.RedirectStandardInput = true;
                    psi.RedirectStandardOutput = true;
                    psi.UseShellExecute = false;
                    psi.WorkingDirectory = filechooserwidget1.CurrentFolder;
                    p = System.Diagnostics.Process.Start(psi);
                    p.WaitForExit(); 
                    string ewwewe = p.StandardOutput.ReadToEnd();*/


                    if (combine_spectracheck.Active)
                    {
                        psi = new System.Diagnostics.ProcessStartInfo("mkdir", "combined");
                        psi.RedirectStandardOutput = true;
                        psi.UseShellExecute = false;
                        psi.WorkingDirectory = filechooserwidget1.CurrentFolder;
                        p = System.Diagnostics.Process.Start(psi);
                        p.WaitForExit();
                    }

                    List<string> filesInFolder = new List<string>();
                    filesInFolder.AddRange(tool_output.Replace("./", "").Split('\n'));
                    filesInFolder.RemoveAll(x => string.IsNullOrEmpty(x));
                    var fileNewTar = p.StandardOutput.ReadToEnd();
                    var fNN = fileNewTar.Replace("./", "").Replace("\n", "");
                    int i = 0;
                    foreach (var archive in filesInFolder)
                    {
                        var dir = archive.Replace(".tar.gz", "");
                        psi = new System.Diagnostics.ProcessStartInfo("mkdir", dir);
                        psi.RedirectStandardOutput = true;
                        psi.UseShellExecute = false;
                        psi.WorkingDirectory = filechooserwidget1.CurrentFolder;
                        p = System.Diagnostics.Process.Start(psi);
                        p.WaitForExit();


                        if (epic_check.Active)
                        {
                            runScript(archive, spectra_textview.Buffer.Text, "spectra.sas",
                                epic_name.Text?.Trim() + i.ToString(), "epic", copiedFiles);
                        }
                        if (mos1check.Active)
                        {
                            runScript(archive, spmos1textview.Buffer.Text, "spmos1.sas",
                                mos1_name.Text?.Trim() + i.ToString(), "_mos1", copiedFiles);
                        }
                        if (mos2check.Active)
                        {
                            runScript(archive, spmos2textview.Buffer.Text, "spmos2.sas",
                                mos2_name.Text?.Trim() + i.ToString(), "_mos2", copiedFiles);
                        }
                        i++;
                    }

                    if (combine_spectracheck.Active)
                    {

                        var script =
                         "epicspeccombine pha=\"" + String.Join(" ", copiedFiles.Where(x => x.Contains(".ds")).ToArray()) + "\" \\"
                        + "\nbkg=\"" + String.Join(" ", copiedFiles.Where(x => x.Contains("bkg.fits")).ToArray()) + "\" \\"
                        + "\nrmf=\"" + String.Join(" ", copiedFiles.Where(x => x.Contains(".rmf")).ToArray()) + "\" \\"
                        + "\narf=\"" + String.Join(" ", copiedFiles.Where(x => x.Contains(".arf")).ToArray()) + "\" \\"
                        + "\nfilepha=\"{name}_src_grp.ds\" filebkg=\"{name}_bkg_grp.bkg\" filersp=\"{name}_resp_grp.rmf\""
                        .Replace("{name}", combined_spectra_name.Text?.Trim());

                        combined_script_textview.Buffer.Text = script;
                        logToConsole("combination script");
                        logToConsole(script);
                    }
                    watch.Stop();
                    var elapsedS = (watch.ElapsedMilliseconds)/1000 ;
                    ShowDialog("All jobs done\n processing time: "+ (elapsedS/60).ToString() + " minutes");
                    button2.Sensitive = true;
                }
                catch (Exception ex)
                {

                    ShowDialog(ex.Message);
                }

            }));
            t.Start();
        }
    }
    public void ShowDialog(string message)
    {
        Gtk.Application.Invoke(delegate {
            using (var md = new MessageDialog(null,
            DialogFlags.Modal, MessageType.Info, ButtonsType.Ok, message))
            {
                md.Title = "SasRunner";
                md.Run();
                md.Destroy();
            }
        });
    }

    public void runScript(string archive, string scriptTxt, string scriptName,
        string nameToreplaceBy, string jobName, List<string> copiedFiles)
    {

        string workdir = filechooserwidget1.CurrentFolder + "/" + archive.Replace(".tar.gz", "");

        logToConsole("\n" + jobName + " begin");

        // extract *.tar.gz file
        List<string> filesTodelete = new List<string>();
        System.Diagnostics.ProcessStartInfo psi
        = new System.Diagnostics.ProcessStartInfo("tar", "-xvf " + archive + " --directory " + workdir);
        psi.RedirectStandardOutput = true;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = filechooserwidget1.CurrentFolder;
        System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
        p.WaitForExit();
        string tool_output = p.StandardOutput.ReadToEnd();
        logToConsole("\n" + archive + " - begin extract");
        filesTodelete.AddRange(tool_output.Replace("./", "").Split('\n'));


        // find new *.TAR file
        psi = new System.Diagnostics.ProcessStartInfo("find", "-type f -name \"*.TAR\"");
        psi.RedirectStandardOutput = true;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = workdir;
        p = System.Diagnostics.Process.Start(psi);
        p.WaitForExit();
        var fileNewTar = p.StandardOutput.ReadToEnd();
        var fNN = fileNewTar.Replace("./", "").Replace("\n", "");

        // extract found tar file
        psi = new System.Diagnostics.ProcessStartInfo("tar", "-xvf " + fNN);
        psi.RedirectStandardOutput = true;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = workdir; ;
        p = System.Diagnostics.Process.Start(psi);
        p.WaitForExit();
        tool_output = p.StandardOutput.ReadToEnd();
        logToConsole("\n" + archive + " - done");
        filesTodelete.AddRange(tool_output.Replace("./", "").Split('\n'));


        // run main script
        logToConsole("\n" + workdir + " - processing begin");
        var script = scriptTxt
        .Replace("{sas_odf_dir}", workdir)
        .Replace("{nametoreplace}", nameToreplaceBy);

        if (combine_spectracheck.Active)
        {
            script = script.Replace("{combine_cp_command}", @"
                    cp {nametoreplace}_spec.fits ../combined 
                    cp {nametoreplace}_bkg.fits ../combined 
                    cp {nametoreplace}.rmf ../combined 
                    cp {nametoreplace}.arf ../combined 
                    cp {nametoreplace}.ds ../combined ")
            .Replace("{nametoreplace}", nameToreplaceBy);

            copiedFiles.Add("{nametoreplace}_spec.fits"
            .Replace("{nametoreplace}", nameToreplaceBy));

            copiedFiles.Add("{nametoreplace}_bkg.fits"
            .Replace("{nametoreplace}", nameToreplaceBy));

            copiedFiles.Add("{nametoreplace}.rmf"
            .Replace("{nametoreplace}", nameToreplaceBy));

            copiedFiles.Add("{nametoreplace}.arf"
            .Replace("{nametoreplace}", nameToreplaceBy));

            copiedFiles.Add("{nametoreplace}.ds"
            .Replace("{nametoreplace}", nameToreplaceBy));
        }
        else
        {
            script = script.Replace("{combine_cp_command}", "");
        }
        System.IO.File.WriteAllText(workdir + '/' + scriptName, script);
        // run sas
        psi = new System.Diagnostics.ProcessStartInfo("/bin/bash", scriptName);
        psi.RedirectStandardOutput = false;
        psi.UseShellExecute = true;
        psi.WorkingDirectory = workdir;
        p = System.Diagnostics.Process.Start(psi);
        p.WaitForExit();
        logToConsole("\n" + workdir + " - processing done");
        foreach (var file in filesTodelete)
        {
            if (!string.IsNullOrWhiteSpace(file) && !string.IsNullOrEmpty(file))
                File.Delete(workdir + '/' + file);
        }
    }

    public void logToConsole(string text)
    {
        System.Diagnostics.ProcessStartInfo
        psi = new System.Diagnostics.ProcessStartInfo("echo", text);
        psi.RedirectStandardOutput = false;
        psi.UseShellExecute = true;
        System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
        p.WaitForExit();
    }
}

