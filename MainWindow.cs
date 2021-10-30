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

    protected void spectraCheck(object sender, EventArgs e)
    {
        hbox3.Visible = combine_spectracheck.Active;
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

            if (threadedRun.Active)
            {
                Thread t = new Thread(()=> mainWorker(false));
                t.Start();
            } 
            else
            {
                mainWorker(false);
            }
        }
    }
    protected void OnRegenerateClicked(object sender, EventArgs e)
    {
        if (threadedRun.Active)
        {
            Thread t = new Thread(() => mainWorker(true));
            t.Start();
        }
        else
        {
            mainWorker(true);
        }
    }
    public void mainWorker(bool regenerate)
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

            if (!regenerate)
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
                if (!regenerate)
                {
                    psi = new System.Diagnostics.ProcessStartInfo("mkdir", dir);
                    psi.RedirectStandardOutput = true;
                    psi.UseShellExecute = false;
                    psi.WorkingDirectory = filechooserwidget1.CurrentFolder;
                    p = System.Diagnostics.Process.Start(psi);
                    p.WaitForExit();
                }
                if (usepredefinedselections.Active && !regenerate)
                {
                    psi = new System.Diagnostics.ProcessStartInfo("cp");
                    psi.Arguments = " -a ./selections/" + dir + "/. " + dir;
                    psi.RedirectStandardOutput = true;
                    psi.UseShellExecute = false;
                    psi.WorkingDirectory = filechooserwidget1.CurrentFolder;
                    p = System.Diagnostics.Process.Start(psi);
                    p.WaitForExit();
                }
                if (epic_check.Active)
                {
                    runScript(archive, spectra_textview.Buffer.Text, "spectra.sas",
                        epic_name.Text?.Trim() + i.ToString(), "epic", copiedFiles, regenerate);
                }
                if (mos1check.Active)
                {
                    runScript(archive, spmos1textview.Buffer.Text, "spmos1.sas",
                        mos1_name.Text?.Trim() + i.ToString(), "_mos1", copiedFiles, regenerate);
                }
                if (mos2check.Active)
                {
                    runScript(archive, spmos2textview.Buffer.Text, "spmos2.sas",
                        mos2_name.Text?.Trim() + i.ToString(), "_mos2", copiedFiles, regenerate);
                }
                combined_script_textview.Buffer.Text += $"\n{i} - {archive} ";
                i++;
            }
            combined_script_textview.Buffer.Text += "\n";
            // combine script
            var script =
             "epicspeccombine pha=\"" + String.Join(" ", copiedFiles.Where(x => x.Contains(".ds")).ToArray()) + "\" \\"
            + "\nbkg=\"" + String.Join(" ", copiedFiles.Where(x => x.Contains("bkg.fits")).ToArray()) + "\" \\"
            + "\nrmf=\"" + String.Join(" ", copiedFiles.Where(x => x.Contains(".rmf")).ToArray()) + "\" \\"
            + "\narf=\"" + String.Join(" ", copiedFiles.Where(x => x.Contains(".arf")).ToArray()) + "\" \\"
            + "\nfilepha=\"{name}_src_grp.ds\" filebkg=\"{name}_bkg_grp.bkg\" filersp=\"{name}_resp_grp.rmf\""
            .Replace("{name}", combined_spectra_name.Text?.Trim());

            combined_script_textview.Buffer.Text +="\n" + script;

            if (runxspec.Active)
            {
                string xspec_xcm = @"
cpd /CPS
setplot energy
data {data}
hardcopy generated.ps color
exit";

                string data = "";
                int counter = 1;
                foreach (var file in copiedFiles.Where(x => x.Contains(".ds")))
                {
                    data += $"{counter}:{counter} { file} ";
                    counter++;
                }
                xspec_xcm = xspec_xcm.Replace("{data}", data);
                System.IO.File.WriteAllText(filechooserwidget1.CurrentFolder + "/combined/xspec.xcm", xspec_xcm);
                string xspecScript = @"
. setsas.sh
xspec - xspec.xcm
xdg-open generated.ps";
                System.IO.File.WriteAllText(filechooserwidget1.CurrentFolder + "/combined/xspec.sh", xspecScript);
                // run sas
                psi = new System.Diagnostics.ProcessStartInfo("/bin/bash", "xspec.sh");
                psi.RedirectStandardOutput = false;
                psi.UseShellExecute = true;
                psi.WorkingDirectory = filechooserwidget1.CurrentFolder + "/combined";
                p = System.Diagnostics.Process.Start(psi);
                p.WaitForExit();
            }
            watch.Stop();
            var elapsedS = (watch.ElapsedMilliseconds) / 1000;
            ShowDialog("All jobs done\n processing time: " + (elapsedS / 60).ToString() + " minutes");
            button2.Sensitive = true;
        }
        catch (Exception ex)
        {

            ShowDialog(ex.Message);
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
        string nameToreplaceBy, string jobName, List<string> copiedFiles, bool regenerate)
    {

        string workdir = filechooserwidget1.CurrentFolder + "/" + archive.Replace(".tar.gz", "");

        logToConsole("\n" + jobName + " begin");
        System.Diagnostics.ProcessStartInfo psi;
        List<string> filesTodelete = new List<string>();
        System.Diagnostics.Process p;
        // extract *.tar.gz file
        if (!regenerate)
        {
            psi = new System.Diagnostics.ProcessStartInfo("tar", "-xvf " + archive + " --directory " + workdir);
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.WorkingDirectory = filechooserwidget1.CurrentFolder;
            p= System.Diagnostics.Process.Start(psi);
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
            psi.WorkingDirectory = workdir;
            p = System.Diagnostics.Process.Start(psi);
            p.WaitForExit();
            tool_output = p.StandardOutput.ReadToEnd();
            logToConsole("\n" + archive + " - done");
            filesTodelete.AddRange(tool_output.Replace("./", "").Split('\n'));
        }

        // run main script
        logToConsole("\n" + workdir + " - processing begin");
        var script = scriptTxt
        .Replace("{sas_odf_dir}", workdir)
        .Replace("{nametoreplace}", nameToreplaceBy);

        if (regenerate)
        {
            script = script.Replace("{multilinecommentbegin}", "<< 'MULTILINE-COMMENT'");
            script = script.Replace("{multilinecommentend}", "MULTILINE-COMMENT");
        }
        else
        {
            script = script.Replace("{multilinecommentbegin}", "");
            script = script.Replace("{multilinecommentend}", "");
        }

        script = script.Replace("{combine_cp_command}", @"
                            cp {nametoreplace}_spec.fits ../combined 
                            cp {nametoreplace}_bkg.fits ../combined 
                            cp {nametoreplace}.rmf ../combined 
                            cp {nametoreplace}.rsp ../combined 
                            cp {nametoreplace}.arf ../combined 
                            cp {nametoreplace}.ds ../combined
                            ")
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

        script = script.Replace("{withspecranges}", combine_spectracheck.Active ? "" : "withspecranges=yes");

        if(runguiapps.Active)
        {
            script = script.Replace("{runguiapps}", "");
        }
        else
        {
            script = script.Replace("{runguiapps}", "#");
        }

        if (combine_spectracheck.Active)
        {
            script = script.Replace("{arfgenNotCombineScript}", "");
            string acceptchanrange = (jobName == "epic" ? "acceptchanrange=yes" : "");
            script = script.Replace("{rmfgenRemainningCombineScript}",
                $"withenergybins=yes energymin={rmfgenEnergyMin.Text} energymax={rmfgenEnergyMax.Text} nenergybins={rmfgennenergybins.Text} {acceptchanrange} ");
        }
            script = script.Replace("{rmfgenRemainningCombineScript}", "");
            if (jobName == "epic")
            {
                script = script.Replace("{arfgenNotCombineScript}",
                    $"badpixlocation=pn_flt_evt.fits detmaptype=psf psfenergy=1.0 extendedsource=no modelee=yes ");
                script = script.Replace("{PATTERN1}", epic_pattern1.Text);
                script = script.Replace("{PATTERN2}", epic_pattern2.Text);
                script = script.Replace("{PATTERN3}", epic_pattern3.Text);
                script = script.Replace("{PATTERN4}", epic_pattern4.Text);
                script = script.Replace("{spectralbinsize}", epic_spectralbinsize.Text);
                script = script.Replace("{timebinsize}", epic_timeBinSize.Text);

        }
            if (jobName == "_mos1")
            {
                script = script.Replace("{arfgenNotCombineScript}",
                    $"badpixlocation=mos1_flt_evt.fits detmaptype=psf ");
                script = script.Replace("{PATTERN1}", mos1_pattern1.Text);
                script = script.Replace("{PATTERN2}", mos1_pattern2.Text);
                script = script.Replace("{PATTERN3}", mos1_pattern3.Text);
                script = script.Replace("{PATTERN4}", mos1_pattern4.Text);
                script = script.Replace("{spectralbinsize}", mos1_spectralbinsize.Text);
                script = script.Replace("{timebinsize}", mos1_timeBinSize.Text);
        }
            if (jobName == "_mos2")
            {
                script = script.Replace("{arfgenNotCombineScript}",
                    $"badpixlocation=mos2_flt_evt.fits detmaptype=psf ");
                script = script.Replace("{PATTERN1}", mos2_pattern1.Text);
                script = script.Replace("{PATTERN2}", mos2_pattern2.Text);
                script = script.Replace("{PATTERN3}", mos2_pattern3.Text);
                script = script.Replace("{PATTERN4}", mos2_pattern4.Text);
                script = script.Replace("{spectralbinsize}", mos2_spectralbinsize1.Text);
                script = script.Replace("{timebinsize}", mos2_timeBinSize.Text);
        }

        System.IO.File.WriteAllText(workdir + "/" + scriptName, script);
        // run sas
        psi = new System.Diagnostics.ProcessStartInfo("/bin/bash", scriptName);
        psi.RedirectStandardOutput = false;
        psi.UseShellExecute = true;
        psi.WorkingDirectory = workdir;
        p = System.Diagnostics.Process.Start(psi);
        p.WaitForExit();
        logToConsole("\n" + workdir + " - processing done");
        if (deleteExtFiles.Active)
        {
            foreach (var file in filesTodelete)
            {
                if (!string.IsNullOrWhiteSpace(file) && !string.IsNullOrEmpty(file))
                    File.Delete(workdir + "/" + file);
            }
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

