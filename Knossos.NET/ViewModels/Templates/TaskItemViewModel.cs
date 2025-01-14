﻿using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Knossos.NET.Models;
using Knossos.NET.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Readers;
using SharpCompress.Common;
using System.Threading;
using System.Net;
using Avalonia;
using Knossos.NET.Classes;
using System.Reflection;
using System.Reflection.PortableExecutable;
using VP.NET;
using System.Text;
using System.Text.Encodings;
using Avalonia.Media;

namespace Knossos.NET.ViewModels
{
    public partial class TaskItemViewModel : ViewModelBase
    {
        [ObservableProperty]
        private bool taskIsSet = false;
        [ObservableProperty]
        private bool cancelButtonVisible = false;
        [ObservableProperty]
        private bool tooltipVisible = false;
        [ObservableProperty]
        private string? tooltip = null;
        [ObservableProperty]
        private string info = string.Empty;
        [ObservableProperty]
        private float progressBarMin = 0;
        [ObservableProperty]
        private float progressBarMax = 0;
        [ObservableProperty]
        private float progressCurrent = 0;
        [ObservableProperty]
        private string name = string.Empty;
        [ObservableProperty]
        private bool isCompleted = false;
        [ObservableProperty]
        private bool isCancelled = false;
        [ObservableProperty]
        private bool isFileDownloadTask = false;
        [ObservableProperty]
        private bool showProgressText = true;
        [ObservableProperty]
        private string currentMirror = string.Empty;
        [ObservableProperty]
        private string pauseButtonText = "Pause";
        [ObservableProperty]
        private IBrush textColor = Brushes.White;
        [ObservableProperty]
        private bool isTextTask = false;

        /* If this task contains subtasks, the subtasks must be added here, from the UIthread */
        [ObservableProperty]
        public ObservableCollection<TaskItemViewModel> taskList = new ObservableCollection<TaskItemViewModel>();
        /* If this task contains subtasks it must be added to this single item list, from the UIthread */
        [ObservableProperty]
        public ObservableCollection<TaskItemViewModel> taskRoot = new ObservableCollection<TaskItemViewModel>();

        private CancellationTokenSource? cancellationTokenSource = null;
        public string? installID = null;
        public string? installVersion = null;
        private bool restartDownload = false;
        private bool pauseDownload = false;

        public TaskItemViewModel() 
        { 
        }

        public void DisplayUpdates(List<Mod> updatedMods)
        {
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    IsCompleted = true;
                    IsTextTask = true;
                    var newMods = updatedMods.Where(x => x.isNewMod && x.type == ModType.mod);
                    var newTCs = updatedMods.Where(x => x.isNewMod && x.type == ModType.tc);
                    var newEngine = updatedMods.Where(x => x.type == ModType.engine);
                    var updateMods = updatedMods.Where(x => !x.isNewMod && x.type != ModType.engine);

                    Name = "Repo Changes:";
                    if(newMods != null && newMods.Any()) 
                    {
                        Name += " New Mods: " + newMods.Count();
                        foreach (var nm in newMods)
                        {
                            var newTask = new TaskItemViewModel();
                            newTask.ShowMsg("Mod Released!   " + nm, null, Brushes.Green);
                            Dispatcher.UIThread.InvokeAsync( () =>
                            {
                                TaskList.Add(newTask);
                            });
                        }
                    }
                    if (newTCs != null && newTCs.Any())
                    {
                        Name += " TCs: " + newTCs.Count();
                        foreach (var nTc in newTCs)
                        {
                            var newTask = new TaskItemViewModel();
                            newTask.ShowMsg("Total Conversion Released!  " + nTc, null, Brushes.Green);
                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                TaskList.Add(newTask);
                            });
                        }
                    }
                    if (newEngine != null && newEngine.Any())
                    {
                        Name += " Engine Builds: " + newEngine.Count();
                        foreach (var ne in newEngine)
                        {

                            var newTask = new TaskItemViewModel();
                            newTask.ShowMsg("Engine Build Released!  " + ne, null, Brushes.Yellow);
                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                TaskList.Add(newTask);
                            });
                        }
                    }
                    if (updateMods != null && updateMods.Any())
                    {
                        Name += " Mod Updates: " + updateMods.Count();
                        foreach (var nm in updateMods)
                        {
                            var newTask = new TaskItemViewModel();
                            newTask.ShowMsg("Mod Update Released!  " + nm, null, Brushes.LightBlue);
                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                TaskList.Add(newTask);
                            });
                        }
                    }
                    Dispatcher.UIThread.InvokeAsync(() => TaskRoot.Add(this));
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch (Exception ex)
            {
                Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.DisplayUpdates()", ex);
            }
        }

        public void ShowMsg(string msg, string? tooltip, IBrush? textColor = null)
        {
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    IsCompleted = true;
                    IsTextTask = true;
                    Name = msg;
                    if (tooltip != null)
                    {
                        Tooltip = tooltip.Trim();
                        TooltipVisible = true;
                    }
                    if(textColor != null)
                    {
                        TextColor = textColor;
                    }
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch (Exception ex)
            {
                Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.ShowMsg()", ex);
            }
        }

        private async Task<bool> DecompressLosseFiles(List<string> filePaths, int alreadySkipped, CancellationTokenSource? cancelSource = null)
        {
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    ProgressBarMax = filePaths.Count();
                    ProgressCurrent = 0;
                    ShowProgressText = false;
                    if (cancelSource != null)
                    {
                        cancellationTokenSource = cancelSource;
                    }
                    else
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }
                    CancelButtonVisible = false;
                    Name = "Decompressing loose files";

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    int skippedCount = alreadySkipped;
                    int decompressedCount = 0;

                    Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.DecompressLosseFiles()", "Starting to decompress loose files");

                    await Parallel.ForEachAsync(filePaths, new ParallelOptions { MaxDegreeOfParallelism = Knossos.globalSettings.compressionMaxParallelism }, async (file, token) =>
                    {
                        var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                        BinaryReader br = new BinaryReader(input);

                        if (!input.CanRead)
                        {
                            input.Dispose();
                            throw new TaskCanceledException();
                        }

                        //Verify if it is compressed
                        if (Encoding.ASCII.GetString(br.ReadBytes(4)) == "LZ41")
                        {
                            FileInfo fi = new FileInfo(file);
                            Info = ProgressCurrent + " / " + ProgressBarMax + " " + fi.Name;
                            input.Seek(0, SeekOrigin.Begin);
                            var output = new FileStream(fi.FullName.ToLower().Replace(".lz41",string.Empty), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                            if (!output.CanWrite)
                            {
                                input.Dispose();
                                output.Dispose();
                                throw new TaskCanceledException();
                            }

                            var compressedSize = await Task.Run(async () => await VPCompression.DecompressStream(input, output));

                            //Delete original
                            input.Dispose();
                            output.Dispose();
                            File.Delete(file);
                            decompressedCount++;
                        }
                        else
                        {
                            skippedCount++;
                        }
                        await input.DisposeAsync();
                        ProgressCurrent++;

                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }
                    });

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    IsCompleted = true;
                    ProgressCurrent = ProgressBarMax;
                    Info = "Decompressed: " + decompressedCount + " Skipped: " + skippedCount;
                    Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.DecompressLosseFiles()", "Decompressing loose files finished: " + Info);
                    return true;
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch (TaskCanceledException)
            {
                /*
                    Task cancel requested by user
                */
                IsCompleted = false;
                IsCancelled = true;
                CancelButtonVisible = false;
                Info = "Task Cancelled";
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                return false;
            }
            catch (Exception ex)
            {
                IsCompleted = false;
                CancelButtonVisible = false;
                IsCancelled = true;
                Info = "Task Failed";
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.DecompressLosseFiles()", ex);
                return false;
            }
        }


        private async Task<bool> CompressLosseFiles(List<string> filePaths, int alreadySkipped, CancellationTokenSource? cancelSource = null)
        {
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    ProgressBarMax = filePaths.Count();
                    ProgressCurrent = 0;
                    ShowProgressText = false;
                    if (cancelSource != null)
                    {
                        cancellationTokenSource = cancelSource;
                    }
                    else
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }
                    CancelButtonVisible = false;
                    Name = "Compressing loose files";

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    int skippedCount = alreadySkipped;
                    int compressedCount = 0;

                    Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.CompressLosseFiles()", "Starting to compress loose files" );

                    await Task.Run(async () => {
                        await Parallel.ForEachAsync(filePaths, new ParallelOptions { MaxDegreeOfParallelism = Knossos.globalSettings.compressionMaxParallelism }, async (file, token) =>
                        {
                            var input = new FileStream(file,FileMode.Open,FileAccess.Read, FileShare.Read);
                            BinaryReader br = new BinaryReader(input);

                            if (!input.CanRead)
                            {
                                input.Dispose();
                                throw new TaskCanceledException();
                            }

                            //Verify if it is compressed
                            if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "LZ41")
                            {
                                FileInfo fi = new FileInfo(file);
                                await Dispatcher.UIThread.InvokeAsync(() => {
                                    Info = ProgressCurrent + " / " + ProgressBarMax + " " + fi.Name;
                                });
                                input.Seek(0, SeekOrigin.Begin);
                                var output = new FileStream(fi.FullName+".lz41", FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                                if(!output.CanWrite)
                                {
                                    input.Dispose();
                                    output.Dispose();
                                    throw new TaskCanceledException();
                                }
                            
                                var compressedSize = await VPCompression.CompressStream(input,output);
                                output.Dispose();
                                if(compressedSize < input.Length)
                                {
                                    //Delete original
                                    input.Dispose();
                                    output.Dispose();
                                    File.Delete(file);
                                    compressedCount++;
                                }
                                else
                                {
                                    //Roll back
                                    input.Dispose();
                                    output.Dispose();
                                    File.Delete(fi.FullName + ".lz41");
                                    skippedCount++;
                                }
                            }
                            else
                            {
                                skippedCount++;
                            }
                            await input.DisposeAsync();
                            ProgressCurrent++;

                            if (cancellationTokenSource.IsCancellationRequested)
                            {
                                throw new TaskCanceledException();
                            }
                        });
                    });

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    IsCompleted = true;
                    ProgressCurrent = ProgressBarMax;
                    Info = "Compressed: "+compressedCount + " Skipped: "+skippedCount;
                    Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.CompressLosseFiles()", "Compressing Loose files finished: "+ Info);
                    return true;
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch (TaskCanceledException)
            {
                /*
                    Task cancel requested by user
                */
                IsCompleted = false;
                IsCancelled = true;
                CancelButtonVisible = false;
                Info = "Task Cancelled";
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                return false;
            }
            catch (Exception ex)
            {
                IsCompleted = false;
                CancelButtonVisible = false;
                IsCancelled = true;
                Info = "Task Failed";
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.CompressLosseFiles()", ex);
                return false;
            }
        }

        private async Task<bool> DecompressVP(FileInfo vpFile, CancellationTokenSource? cancelSource = null)
        {
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    ProgressBarMax = 1;
                    ProgressCurrent = 0;
                    ShowProgressText = false;
                    if (cancelSource != null)
                    {
                        cancellationTokenSource = cancelSource;
                    }
                    else
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }
                    CancelButtonVisible = false;
                    Name = "Decompressing: " + vpFile.Name;

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.DecompressVP()", "Starting to decompress VP file: " + vpFile.Name);

                    await Task.Run(async () => {
                        var vp = new VPContainer();
                        await vp.LoadVP(vpFile.FullName);
                        vp.DisableCompression();
                        await vp.SaveAsAsync(vpFile.FullName.ToLower().Replace(".vpc", ".vp"), compressionCallback, cancellationTokenSource);
                    });

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            File.Delete(vpFile.FullName.ToLower().Replace(".vpc", ".vp"));
                            File.Delete(vpFile.FullName.ToLower().Replace(".vpc", ".vp") + ".tmp");
                        }
                        catch { }
                        throw new TaskCanceledException();
                    }
                    File.Delete(vpFile.FullName);
                    Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.DecompressVP()", "Decompress VP finished: " + vpFile.Name + " Processed Files: " + ProgressBarMax);
                    Info = "";
                    IsCompleted = true;
                    ProgressCurrent = ProgressBarMax;
                    return true;
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch (TaskCanceledException)
            {
                /*
                    Task cancel requested by user
                */
                IsCompleted = false;
                IsCancelled = true;
                CancelButtonVisible = false;
                Info = "Task Cancelled";
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                return false;
            }
            catch (Exception ex)
            {
                IsCompleted = false;
                CancelButtonVisible = false;
                IsCancelled = true;
                Info = "Task Failed";
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.DecompressVP()", ex);
                return false;
            }
        }

        private async Task<bool> CompressVP(FileInfo vpFile, CancellationTokenSource? cancelSource = null)
        {
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    ProgressBarMax = 1;
                    ProgressCurrent = 0;
                    ShowProgressText = false;
                    if (cancelSource != null)
                    {
                        cancellationTokenSource = cancelSource;
                    }
                    else
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }
                    CancelButtonVisible = false;
                    Name = "Compressing: " + vpFile.Name;

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.CompressVP()", "Starting to compress VP file: " + vpFile.Name);

                    await Task.Run(async() => {
                        var vp = new VPContainer();
                        await vp.LoadVP(vpFile.FullName);
                        vp.EnableCompression();
                        await vp.SaveAsAsync(vpFile.FullName + "c", compressionCallback, cancellationTokenSource);
                    });

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            File.Delete(vpFile.FullName + "c");
                            File.Delete(vpFile.FullName + "c.tmp");
                        }
                        catch { }
                        throw new TaskCanceledException();
                    }
                    File.Delete(vpFile.FullName);

                    Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.CompressVP()", "Compress VP finished: " + vpFile.Name + " Processed Files: " + ProgressBarMax );
                    Info = "";
                    IsCompleted = true;
                    ProgressCurrent = ProgressBarMax;
                    return true;
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch (TaskCanceledException)
            {
                /*
                    Task cancel requested by user
                */
                IsCompleted = false;
                IsCancelled = true;
                CancelButtonVisible = false;
                Info = "Task Cancelled";
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                return false;
            }
            catch (Exception ex)
            {
                IsCompleted = false;
                CancelButtonVisible = false;
                IsCancelled = true;
                Info = "Task Failed";
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.CompressVP()", ex);
                return false;
            }
        }

        private async void compressionCallback(string filename, int maxFiles)
        {
            if (filename != string.Empty)
            {
                ProgressCurrent++;
            }
            else
            {
                ProgressCurrent = 0;
            }
            await Dispatcher.UIThread.InvokeAsync(() => {
                ProgressBarMax = maxFiles;
                Info = ProgressCurrent.ToString() + " / " + ProgressBarMax.ToString() + "  -  " + filename;
            });
        }

        public async Task<bool> CompressMod(Mod mod, CancellationTokenSource? cancelSource = null, bool isSubTask = false)
        {
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    if(!isSubTask)
                    {
                        CancelButtonVisible = true;
                        Name = "Compressing mod: " + mod.title + " " + mod.version;
                    }
                    else
                    {
                        Name = "Compressing mod";
                    }
                    
                    ShowProgressText = false;
                    await Dispatcher.UIThread.InvokeAsync(() => {
                        TaskRoot.Add(this);
                    });
                    ProgressBarMin = 0;
                    ProgressCurrent = 0;
                    Info = "In Queue";

                    if (cancelSource != null)
                    {
                        cancellationTokenSource = cancelSource;
                    }
                    else
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }

                    //Wait in Queue
                    if (!isSubTask)
                    {
                        while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                        {
                            await Task.Delay(1000);
                            if (cancellationTokenSource.IsCancellationRequested)
                            {
                                throw new TaskCanceledException();
                            }
                        }
                    }

                    Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.CompressMod()", "Starting to compress Mod: " + mod.title);

                    var vpFiles = Directory.GetFiles(mod.fullPath, "*.vp").ToList();
                    ProgressBarMax = vpFiles.Count()+1;

                    //Loose Files Compression
                    if(Directory.Exists(mod.fullPath+Path.DirectorySeparatorChar+"data") || mod.devMode)
                    {
                        var searchDir = mod.devMode ? mod.fullPath : mod.fullPath + Path.DirectorySeparatorChar + "data";
                        var allFilesInDataFolder = Directory.GetFiles(searchDir, "*.*",SearchOption.AllDirectories).ToList();
                        int skipped = 0;
                        //Filter
                        foreach(var fileInData in allFilesInDataFolder.ToList())
                        {
                            var file = new FileInfo(fileInData);

                            if (file.IsReadOnly || file.Length < VPCompression.MinimumSize || VPCompression.ExtensionIgnoreList.Contains(file.Extension.ToLower()) || file.Extension.ToLower() == ".lz41") 
                            { 
                                if(file.Extension.ToLower() == ".vp")
                                {
                                    vpFiles.Add(fileInData);
                                    ProgressBarMax++;
                                }
                                allFilesInDataFolder.Remove(fileInData);
                                skipped++;
                            }
                        }
                        //Process
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var fileTask = new TaskItemViewModel();
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                TaskList.Insert(0, fileTask);
                            });
                            
                            Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;

                            var result = await fileTask.CompressLosseFiles(allFilesInDataFolder, skipped, cancellationTokenSource);
                            if (cancellationTokenSource.IsCancellationRequested)
                            {
                                throw new TaskCanceledException();
                            }
                        },DispatcherPriority.Background);
                    }
                    ProgressCurrent++;
                    Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;

                    //VP Compression
                    await Parallel.ForEachAsync(vpFiles, new ParallelOptions { MaxDegreeOfParallelism = Knossos.globalSettings.compressionMaxParallelism }, async (file, token) =>
                    {
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var vpTask = new TaskItemViewModel();
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                TaskList.Insert(0, vpTask);
                            });
                            Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;
                            await vpTask.CompressVP(new FileInfo(file), cancellationTokenSource);
                            ProgressCurrent++;
                            Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;
                            if (cancellationTokenSource.IsCancellationRequested)
                            {
                                throw new TaskCanceledException();
                            }
                        },DispatcherPriority.Background);
                    });

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    //Update settings json
                    mod.modSettings.Load(mod.fullPath);
                    mod.modSettings.isCompressed = true;
                    mod.modSettings.Save();

                    IsCompleted = true;
                    ProgressCurrent = ProgressBarMax;
                    Info = string.Empty;
                    CancelButtonVisible = false;

                    if (!isSubTask && TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                    {
                        TaskViewModel.Instance!.taskQueue.Dequeue();
                    }

                    return true;
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch (TaskCanceledException)
            {
                Info = "Task Cancelled";
                IsCompleted = false;
                CancelButtonVisible = false;
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                if (!isSubTask)
                {
                    while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                    {
                        await Task.Delay(500);
                    }
                    if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                    {
                        TaskViewModel.Instance!.taskQueue.Dequeue();
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Info = "Task Failed";
                IsCompleted = false;
                CancelButtonVisible = false;
                cancellationTokenSource?.Cancel();
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                if (!isSubTask)
                {
                    while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                    {
                        await Task.Delay(500);
                    }
                    if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                    {
                        TaskViewModel.Instance!.taskQueue.Dequeue();
                    }
                }
                Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.CompressMod()", ex);
                return false;
            }
        }

        public async Task<bool> DecompressMod(Mod mod, CancellationTokenSource? cancelSource = null)
        {
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    CancelButtonVisible = true;
                    Name = "Decompressing mod: " + mod.title + " " + mod.version;
                    ShowProgressText = false;
                    await Dispatcher.UIThread.InvokeAsync(() => TaskRoot.Add(this));
                    ProgressBarMin = 0;
                    ProgressCurrent = 0;
                    Info = "In Queue";

                    if (cancelSource != null)
                    {
                        cancellationTokenSource = cancelSource;
                    }
                    else
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }

                    //Wait in Queue
                    while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                    {
                        await Task.Delay(1000);
                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }
                    }

                    Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.DecompressMod()", "Starting to decompress Mod: " + mod.title);

                    var vpcFiles = Directory.GetFiles(mod.fullPath, "*.vpc").ToList();
                    ProgressBarMax = vpcFiles.Count() + 1;

                    //Loose Files Compression
                    if (Directory.Exists(mod.fullPath + Path.DirectorySeparatorChar + "data") || mod.devMode)
                    {
                        var searchDir = mod.devMode ? mod.fullPath : mod.fullPath + Path.DirectorySeparatorChar + "data";
                        var allFilesInDataFolder = Directory.GetFiles(searchDir, "*.*", SearchOption.AllDirectories).ToList();
                        int skipped = 0;
                        //Filter
                        foreach (var fileInData in allFilesInDataFolder.ToList())
                        {
                            var file = new FileInfo(fileInData);

                            if (file.IsReadOnly || file.Extension.ToLower() != ".lz41")
                            {
                                if (file.Extension.ToLower() == ".vpc")
                                {
                                    vpcFiles.Add(fileInData);
                                    ProgressBarMax++;
                                }

                                allFilesInDataFolder.Remove(fileInData);
                                skipped++;
                            }
                        }
                        //Process
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var fileTask = new TaskItemViewModel();
                            await Dispatcher.UIThread.InvokeAsync(() => { TaskList.Insert(0, fileTask); });
                            Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;

                            var result = await fileTask.DecompressLosseFiles(allFilesInDataFolder, skipped, cancellationTokenSource);
                            if (!result || cancellationTokenSource.IsCancellationRequested)
                            {
                                throw new TaskCanceledException();
                            }
                        },DispatcherPriority.Background);
                    }
                    ProgressCurrent++;
                    Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;

                    //VPC Decompression
                    await Parallel.ForEachAsync(vpcFiles, new ParallelOptions { MaxDegreeOfParallelism = Knossos.globalSettings.compressionMaxParallelism }, async (file, token) =>
                    {
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            var vpTask = new TaskItemViewModel();
                            await Dispatcher.UIThread.InvokeAsync(() => { TaskList.Insert(0, vpTask); });
                            Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;
                            await vpTask.DecompressVP(new FileInfo(file), cancellationTokenSource);
                            ProgressCurrent++;
                            Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;
                            if (cancellationTokenSource.IsCancellationRequested)
                            {
                                throw new TaskCanceledException();
                            }
                        },DispatcherPriority.Background);
                    });

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    //Update settings json
                    mod.modSettings.Load(mod.fullPath);
                    mod.modSettings.isCompressed = false;
                    mod.modSettings.Save();

                    IsCompleted = true;
                    ProgressCurrent = ProgressBarMax;
                    Info = string.Empty;
                    CancelButtonVisible = false;

                    if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                    {
                        TaskViewModel.Instance!.taskQueue.Dequeue();
                    }

                    return true;
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch (TaskCanceledException)
            {
                Info = "Task Cancelled";
                IsCompleted = false;
                CancelButtonVisible = false;
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                {
                    await Task.Delay(500);
                }
                if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                {
                    TaskViewModel.Instance!.taskQueue.Dequeue();
                }
                return false;
            }
            catch (Exception ex)
            {
                Info = "Task Failed";
                IsCompleted = false;
                CancelButtonVisible = false;
                cancellationTokenSource?.Cancel();
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                {
                    await Task.Delay(500);
                }
                if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                {
                    TaskViewModel.Instance!.taskQueue.Dequeue();
                }
                Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.CompressMod()", ex);
                return false;
            }
        }

        public async Task<bool> DecompressNebulaFile(string filepath, string? filename, string dest, CancellationTokenSource? cancelSource = null)
        {
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    CancelButtonVisible = false;
                    Name = "Decompressing " + filename;
                    ShowProgressText = false;
                    ProgressBarMin = 0;
                    ProgressCurrent = 0;
                    if (cancelSource != null)
                    {
                        cancellationTokenSource = cancelSource;
                    }
                    else
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }
                    //tar.gz
                    if (filename!.ToLower().Contains(".tar") || filename.ToLower().Contains(".gz"))
                    {
                        await Task.Run(() =>
                        {
                            using var fileStream = File.OpenRead(filepath);
                            using (var reader = ReaderFactory.Open(fileStream))
                            {
                                try
                                {
                                    ProgressCurrent = 0;
                                    ProgressBarMax = 1;
                                    while (reader.MoveToNextEntry())
                                    {

                                        if (!reader.Entry.IsDirectory)
                                        {
                                            reader.WriteEntryToDirectory(dest, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true, WriteSymbolicLink = (source, target) => { File.CreateSymbolicLink(source, target); } });
                                        }
                                        if (cancellationTokenSource!.IsCancellationRequested)
                                        {
                                            throw new TaskCanceledException();
                                        }
                                    }
                                    IsCompleted = true;
                                    Info = string.Empty;
                                    ProgressCurrent = ProgressBarMax;
                                    fileStream.Close();
                                    return true;
                                }
                                catch (TaskCanceledException)
                                {
                                    Info = "Task Cancelled";
                                    IsCancelled = true;
                                    fileStream.Close();
                                    return false;
                                }
                                catch (Exception ex)
                                {
                                    Info = "Task Failed";
                                    IsCompleted = false;
                                    IsCancelled = true;
                                    CancelButtonVisible = false;
                                    cancellationTokenSource?.Cancel();
                                    fileStream.Close();
                                    Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.DecompressTask()", ex);
                                    return false;
                                }
                            }
                        });
                    }
                    else
                    {
                        //zip, 7z
                        await Task.Run(() => {
                            using (var archive = ArchiveFactory.Open(filepath))
                            {
                                try
                                {
                                    var reader = archive.ExtractAllEntries();
                                    ProgressBarMax = archive.Entries.Count();
                                    while (reader.MoveToNextEntry())
                                    {
                                        Info = "Files: " + ProgressCurrent + "/" + ProgressBarMax;
                                        if (!reader.Entry.IsDirectory)
                                        {
                                            reader.WriteEntryToDirectory(dest, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                                        }
                                        ProgressCurrent++;
                                        if (cancellationTokenSource!.IsCancellationRequested)
                                        {
                                            throw new TaskCanceledException();
                                        }
                                    }
                                    IsCompleted = true;
                                    Info = string.Empty;
                                    ProgressCurrent = ProgressBarMax;
                                    return true;
                                }
                                catch (TaskCanceledException)
                                {
                                    Info = "Task Cancelled";
                                    IsCancelled = true;
                                    return false;
                                }
                                catch (Exception ex)
                                {
                                    Info = "Task Failed";
                                    IsCompleted = false;
                                    IsCancelled = true;
                                    CancelButtonVisible = false;
                                    cancellationTokenSource?.Cancel();
                                    Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.DecompressTask()", ex);
                                    return false;
                                }
                            }
                        });
                    }
                    return true;
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch(TaskCanceledException)
            {
                Info = "Task Cancelled";
                IsCompleted = false;
                CancelButtonVisible = false;
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                return false;
            }
            catch(Exception ex)
            {
                Info = "Task Failed";
                IsCompleted = false;
                CancelButtonVisible = false;
                cancellationTokenSource?.Cancel();
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.DecompressTask()", ex);
                return false;
            }
        }

        public async Task<bool> VerifyMod(Mod mod, CancellationTokenSource cancelSource)
        {
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    installVersion = mod.version;
                    installID = mod.id;
                    if (cancelSource != null)
                    {
                        cancellationTokenSource = cancelSource;
                    }
                    else
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }
                    CancelButtonVisible = true;
                    Name = "Verifying " + mod.ToString();
                    ShowProgressText = false;
                    await Dispatcher.UIThread.InvokeAsync(() => TaskRoot.Add(this));
                    Info = "In Queue";

                    //Wait in Queue
                    while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                    {
                        await Task.Delay(1000);
                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }
                    }

                    Info = "Starting";

                    ProgressCurrent = 0;
                    ProgressBarMax = 0;
                    foreach (var pkg in mod.packages)
                    {
                        if(pkg.filelist!=null)
                        {
                            ProgressBarMax += pkg.filelist.Count();
                        }
                    }

                    Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.VerifyMod()", "Start verify for :" + mod);
                    mod.ReLoadJson();
                    List<ModPackage> reinstall = new List<ModPackage>();
                    List<string> fileArray = Directory.GetFiles(mod.fullPath, "*.*", SearchOption.AllDirectories).ToList();
                    for (int i = fileArray.Count() - 1; i >= 0; i--)
                    {
                        if (fileArray[i].ToLower().Contains(".json") || fileArray[i].ToLower().Contains(".ini") || mod.tile!= null && fileArray[i].ToLower().Contains(mod.tile) || mod.banner!=null && fileArray[i].ToLower().Contains(mod.banner) || fileArray[i].ToLower().Contains("kn_screen"))
                            fileArray.RemoveAt(i);
                    }
                    foreach (var pkg in mod.packages)
                    {
                        bool pkgPassed = true;
                        if (pkg.filelist != null)
                        {
                            foreach (var file in pkg.filelist)
                            {
                                if (cancellationTokenSource!.IsCancellationRequested)
                                {
                                    throw new TaskCanceledException();
                                }
                                for (int i = fileArray.Count() - 1; i >= 0; i--)
                                {
                                    if (fileArray[i].ToLower().Replace(Path.DirectorySeparatorChar.ToString(),"").Contains(file.filename!.ToLower().Replace(@"./", "").Replace(@"\", "").Replace(@"/", "")))
                                        fileArray.RemoveAt(i);
                                }
                                ProgressCurrent++;
                                Info = "Files: "+ ProgressCurrent + "/"+ ProgressBarMax + " Current File: " + file.filename;
                                //Checksum
                                if (file.checksum != null && file.checksum.Count() > 0)
                                {
                                    if (file.checksum[0].ToLower() == "sha256")
                                    {
                                        try
                                        {
                                            using (FileStream? filehash = new FileStream(mod.fullPath + Path.DirectorySeparatorChar + file.filename, FileMode.Open, FileAccess.Read))
                                            {
                                                using (SHA256 checksum = SHA256.Create())
                                                {
                                                    filehash.Position = 0;
                                                    var hashValue = BitConverter.ToString(await checksum.ComputeHashAsync(filehash)).Replace("-", String.Empty);
                                                    filehash.Close();
                                                    if (hashValue.ToLower() != file.checksum[1].ToLower())
                                                    {
                                                        pkgPassed = false;
                                                        Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.VerifyMod()", file.filename + " failed checksum check! Mod: " + mod);
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            //Filenotfound most likely
                                            pkgPassed = false;
                                            Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.VerifyMod()", ex);
                                        }
                                    }
                                    else
                                    {
                                        Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.VerifyMod()", "Unsupported checksum crypto: " + file.checksum[0]);
                                    }
                                }
                                if(!pkgPassed)
                                {
                                    continue;
                                }
                            }
                        }
                        if (pkgPassed)
                        {
                            Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.VerifyMod()", "Pkg Verify OK: "+ pkg.name +"Mod: " + mod);
                        }
                        else
                        {
                            pkg.isSelected = true;
                            reinstall.Add(pkg);
                            Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.VerifyMod()", "Pkg Verify FAIL: " + pkg.name + "Mod: " + mod);
                        }
                    }

                    if (cancellationTokenSource!.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                    {
                        TaskViewModel.Instance!.taskQueue.Dequeue();
                    }

                    IsCompleted = true;
                    CancelButtonVisible = false;
                    ProgressCurrent = ProgressBarMax;

                    if (!reinstall.Any())
                    {
                        Info = "PASSED";
                        mod.ClearUnusedData();
                    }
                    else
                    {
                        Info = "FAIL";
                        TaskViewModel.Instance?.InstallMod(mod,reinstall);
                    }

                    if(fileArray.Any())
                    {
                        foreach (var file in fileArray)
                        {
                            Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.VerifyMod()", "Unknown file detected during verify: " + file);
                        }
                        Info += " - " + fileArray.Count() + " Unknown files detected, check log or debug console";
                    }

                    return true;
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch (TaskCanceledException)
            {
                /*
                    Task cancel requested by user
                */
                IsCompleted = false;
                IsCancelled = true;
                CancelButtonVisible = false;
                Info = "Cancel Requested";
                while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                {
                    await Task.Delay(500);
                }
                if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                {
                    TaskViewModel.Instance!.taskQueue.Dequeue();
                }
                await Task.Delay(2000); //give time for child tasks to cancel first
                Info = "Task Cancelled";
                mod.ClearUnusedData();
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                return false;
            }
            catch (Exception ex)
            {
                /*
                    Task cancel forced due to a error
                */
                IsCompleted = false;
                IsCancelled = true;
                CancelButtonVisible = false;
                cancellationTokenSource?.Cancel();
                Info = "Cancel Requested";
                while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                {
                    await Task.Delay(500);
                }
                if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                {
                    TaskViewModel.Instance!.taskQueue.Dequeue();
                }

                await Task.Delay(2000); //give time for child tasks to cancel first
                Info = "Task Failed";
                mod.ClearUnusedData();
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.VerifyMod()", ex);
                return false;
            }
        }

        public async Task<bool> InstallMod(Mod mod, CancellationTokenSource cancelSource, List<ModPackage>? reinstallPkgs = null, bool manualCompress = false)
        {
            string? modPath = null;
            Mod? installed = null;
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    installVersion = mod.version;
                    installID = mod.id;
                    if (cancelSource != null)
                    {
                        cancellationTokenSource = cancelSource;
                    }
                    else
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }
                    CancelButtonVisible = true;
                    Name = "Downloading " + mod.ToString();
                    ShowProgressText = false;
                    await Dispatcher.UIThread.InvokeAsync(() => TaskRoot.Add(this));
                    Info = "In Queue";
                    bool compressMod = false;

                    //Set Mod card as "installing"
                    MainWindowViewModel.Instance?.NebulaModsView.SetInstalling(mod.id, cancellationTokenSource);

                    //Wait in Queue
                    while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                    {
                        await Task.Delay(1000);
                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }
                    }

                    compressMod = manualCompress;
                    //Todo add mod fso version checking
                    if (!mod.devMode && Knossos.globalSettings.modCompression == CompressionSettings.Always)
                    {
                        compressMod = true;
                    }
                    if (!mod.devMode && Knossos.globalSettings.modCompression == CompressionSettings.ModSupport)
                    {
                        try
                        {
                            var fso = mod.GetDependency("FSO");
                            if (fso != null && (fso.version == null || fso.version.Contains(">=") || SemanticVersion.Compare(fso.version.Replace(">=", "").Replace("<", "").Replace(">", "").Trim(), VPCompression.MinimumFSOVersion) > 0))
                                compressMod = true;
                        }catch (Exception ex)
                        {
                            Log.Add(Log.LogSeverity.Error,"Knossos.PlayMod()", ex);
                        }
                    }

                    /*
                        Check if its installed, even on install task it could have been installed by another task that was in the queue
                    */
                    installed = Knossos.GetInstalledMod(mod.id, mod.version);
                    if (installed != null) 
                    {
                        Name = "Modify " + mod.ToString();
                        compressMod = installed.modSettings.isCompressed;
                    }

                    Info = "Starting";

                    /*
                        -Parse all files we need to download
                        -Determine mod folder path and rootpack
                        -Add all "ModFile" to a new list for easy access
                        -Create all folders
                        -Create the download token on the root of the mod.
                        -Set all the data needed here, number of tasks, etc for the progress bar and info
                        -Main progress max value is calculated as follows: ( Number of files to download * 2 ) + 2
                         (Download, Decompression, Download banner/tile images)
                        -+1 task if we have to compress
                        -If the mod is installeds there is no need to download the baners and title image again so -2 to max tasks
                    */

                    List<ModFile> files = new List<ModFile>();
                    string modFolder = mod.id + "-" + mod.version;
                    string rootPack = string.Empty;
                    if(mod.type == ModType.tc && mod.parent == null)
                    {
                        rootPack = mod.id;
                    }
                    else
                    {
                        if(mod.type == ModType.mod && mod.parent != null)
                        {
                            rootPack = mod.parent;
                        }
                        else
                        {
                            Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.InstallMod()", "Unable to determine mod root pack " + mod.ToString() + " Type: " + mod.type + " Parent: " + mod.parent);
                            throw new TaskCanceledException();
                        }
                    }

                    modPath = Knossos.GetKnossosLibraryPath() + Path.DirectorySeparatorChar +rootPack + Path.DirectorySeparatorChar + modFolder;
                    for (int i = mod.packages.Count - 1; i >= 0; i--)
                    {
                        bool alreadyInstalled = false;
                        if(installed!=null)
                        {
                            foreach(var pkg in installed.packages)
                            {
                                if(pkg.name == mod.packages[i].name)
                                {
                                    if (reinstallPkgs == null || !reinstallPkgs.Where(re => re.name == mod.packages[i].name).Any())
                                    {
                                        alreadyInstalled = true;
                                        continue;
                                    }
                                }
                            }
                        }
                        if (mod.packages[i].isSelected && !alreadyInstalled)
                        {
                            files.AddRange(mod.packages[i].files!);
                        }
                        else
                        {
                            mod.packages.RemoveAt(i);
                        }
                    }

                    /* Is there is nothing new to install just end the task */
                    if(files.Count == 0)
                    {
                        Info = string.Empty;
                        IsCompleted = true;
                        CancelButtonVisible = false;
                        if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                        {
                            TaskViewModel.Instance!.taskQueue.Dequeue();
                        }
                        return true;
                    }

                    Directory.CreateDirectory(modPath);

                    foreach (var file in files)
                    {
                        if (file.dest != null && file.dest.Trim() != string.Empty)
                        {
                            var path = file.dest;
                            Directory.CreateDirectory(modPath + Path.DirectorySeparatorChar + path);
                        }
                    }

                    ProgressBarMin = 0;
                    ProgressCurrent = 0;
                    ProgressBarMax = installed == null ? (files.Count * 2) + 2 : (files.Count * 2);
                    if(compressMod)
                    {
                        ProgressBarMax += 1;
                    }
                    Info = "Tasks: 0/" + ProgressBarMax;

                    /* Do not create the token on mod updates */
                    if (installed == null)
                    {
                        try
                        {
                            File.Create(modPath + Path.DirectorySeparatorChar + "knossos_net_download.token").Close();
                        }
                        catch { }
                    }

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    /*
                        -Use parallel to process this new list, the max parallelism is the max number of concurrent downloads
                        -Always check canceltask before executing something
                        -Download File -> Verify Checksum -> Extract file
                        -Increase main progress when: 
                         File starts to download, File finishes downloading, Decompression starts, Decompression ends, Image download completed
                    */
                    await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = Knossos.globalSettings.maxConcurrentSubtasks }, async (file, token) =>
                    {
                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }

                        //Download
                        var fileTask = new TaskItemViewModel();
                        await Dispatcher.UIThread.InvokeAsync(() => TaskList.Insert(0, fileTask));
                        if (file.dest == null)
                        {
                            file.dest = string.Empty;
                        }
                        Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;
                        var fileFullPath = modPath + Path.DirectorySeparatorChar + file.filename;
                        var result = await fileTask.DownloadFile(file.urls!, fileFullPath, "Downloading " + file.filename, false, null, cancellationTokenSource);

                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }

                        if (result.HasValue && result.Value)
                        {
                            ++ProgressCurrent;
                            Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;
                        }
                        else
                        {
                            throw new Exception("Error while downloading file: " + fileFullPath);
                        }

                        //Checksum
                        if (file.checksum != null && file.checksum.Count() > 0)
                        {
                            if (file.checksum[0].ToLower() == "sha256")
                            {
                                using (FileStream? filehash = new FileStream(fileFullPath, FileMode.Open, FileAccess.Read))
                                {
                                    using (SHA256 checksum = SHA256.Create())
                                    {
                                        filehash.Position = 0;
                                        var hashValue = BitConverter.ToString(await checksum.ComputeHashAsync(filehash)).Replace("-", String.Empty);
                                        filehash.Close();
                                        if (hashValue.ToLower() != file.checksum[1].ToLower())
                                        {
                                            throw new Exception("The downloaded file hash was incorrect, expected: " + file.checksum[1] + " Calculated Hash: " + hashValue);
                                        }
                                    }
                                    fileTask.Info = " Checksum OK!";
                                }
                            }
                            else
                            {
                                Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.InstallMod()", "Unsupported checksum crypto, skipping checksum check :" + file.checksum[0]);
                            }
                        }

                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }

                        //Decompress
                        var decompressTask = new TaskItemViewModel();
                        await Dispatcher.UIThread.InvokeAsync(() => TaskList.Insert(0, decompressTask));
                        var decompResult = await decompressTask.DecompressNebulaFile(fileFullPath, file.filename, modPath + Path.DirectorySeparatorChar + file.dest, cancellationTokenSource);
                        if (!decompResult)
                        {
                            Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.InstallMod()", "Error while decompressing the file " + fileFullPath);
                            CancelTaskCommand();
                        }
                        File.Delete(fileFullPath);
                        ++ProgressCurrent;
                        Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;
                    });
                    files.Clear();

                    /*
                        -Delete the download token.
                        -Download tile and banner images, update those file names on json, increase progress
                        -Add folder name and full path to the modJson before creating the fsobuild
                        -Set modJson installed to true before saving json
                        -Serialize json to folder
                        -Create the FsoBuild object and add it to the main list
                        -Return the same FsoObject so it can be updated on the FsoBuildView
                        -Compress Mod if we had to
                    */

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    if (!string.IsNullOrEmpty(mod.tile) && installed == null)
                    {
                        var tileTask = new TaskItemViewModel();
                        await Dispatcher.UIThread.InvokeAsync(() => TaskList.Insert(0, tileTask));
                        await tileTask.DownloadFile(mod.tile, modPath + Path.DirectorySeparatorChar + "kn_tile.png", "Downloading tile image", false, null, cancellationTokenSource);
                        mod.tile = "kn_tile.png";
                    }
                    ++ProgressCurrent;
                    Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    if (!string.IsNullOrEmpty(mod.banner) && installed == null)
                    {
                        var bannerTask = new TaskItemViewModel();
                        await Dispatcher.UIThread.InvokeAsync(() => TaskList.Insert(0, bannerTask));
                        await bannerTask.DownloadFile(mod.banner, modPath + Path.DirectorySeparatorChar + "kn_banner.png", "Downloading banner image", false, null, cancellationTokenSource);
                        mod.banner = "kn_banner.png";
                    }

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    ++ProgressCurrent;
                    Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;
                    mod.fullPath = modPath + Path.DirectorySeparatorChar;
                    mod.folderName = modFolder;
                    mod.installed = true;

                    if (installed == null)
                    {
                        mod.SaveJson();
                    }
                    else
                    {
                        installed.ReLoadJson();
                        if (reinstallPkgs == null)
                        {
                            installed.packages.AddRange(mod.packages);
                        }
                        installed.SaveJson();
                        installed.ClearUnusedData();
                    }

                    //We have to compress?
                    if (compressMod)
                    {
                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }

                        var cpTask = new TaskItemViewModel();
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            TaskList.Insert(0, cpTask);
                        });
                        await cpTask.CompressMod(mod, cancellationTokenSource, true);
                        ProgressCurrent++;
                        Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;
                    }

                    try
                    {
                        File.Delete(mod.fullPath + Path.DirectorySeparatorChar + "knossos_net_download.token");
                    }
                    catch {}

                    //Remove Mod card, unmark update avalible, re-run dependencies checks
                    if (installed == null)
                    {
                        MainWindowViewModel.Instance?.NebulaModsView.RemoveMod(mod.id);
                        Knossos.AddMod(mod);
                        await Dispatcher.UIThread.InvokeAsync(() => MainWindowViewModel.Instance?.AddInstalledMod(mod), DispatcherPriority.Background);
                        await Dispatcher.UIThread.InvokeAsync(() => MainWindowViewModel.Instance?.MarkAsUpdateAvalible(mod.id, false), DispatcherPriority.Background);
                        MainWindowViewModel.Instance?.RunModStatusChecks();
                    }

                    /*
                        Always Dequeue, always check for check size and verify that the first is this TaskItemViewModel object
                    */
                    if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                    {
                        TaskViewModel.Instance!.taskQueue.Dequeue();
                    }

                    Info = string.Empty;
                    IsCompleted = true;
                    CancelButtonVisible = false;

                    return true;
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch (TaskCanceledException)
            {
                /*
                    Task cancel requested by user
                */
                IsCompleted = false;
                IsCancelled = true;
                CancelButtonVisible = false;
                Info = "Cancel Requested";
                while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                {
                    await Task.Delay(500);
                }
                if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                {
                    TaskViewModel.Instance!.taskQueue.Dequeue();
                }
                await Task.Delay(2000); //give time for child tasks to cancel first
                Info = "Task Cancelled";
                try
                {
                    /* If a error ocurred while updating do not delete the whole mod */
                    if (modPath != null && installed == null)
                    {
                        Directory.Delete(modPath, true);
                    }
                }
                catch { }
                await Dispatcher.UIThread.InvokeAsync(() => MainWindowViewModel.Instance?.CancelModInstall(mod.id), DispatcherPriority.Background);
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                return false;
            }
            catch (Exception ex)
            {
                /*
                    Task cancel forced due to a error
                */
                IsCompleted = false;
                IsCancelled = true;
                CancelButtonVisible = false;
                cancellationTokenSource?.Cancel();
                Info = "Cancel Requested";
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    //Messagebox is not thread safe!
                    await MessageBox.Show(MainWindow.instance!, "An error was ocurred during the download of the mod: " + mod.ToString() + ". Error: " + ex.Message, "Error", MessageBox.MessageBoxButtons.OK);
                });
                while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                {
                    await Task.Delay(500);
                }
                if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                {
                    TaskViewModel.Instance!.taskQueue.Dequeue();
                }

                await Task.Delay(2000); //give time for child tasks to cancel first
                Info = "Task Failed";
                try
                {
                    /* If a error ocurred while updating do not delete the whole mod */
                    if (modPath != null && installed == null)
                    {
                        Directory.Delete(modPath, true);
                    }
                }
                catch { }
                await Dispatcher.UIThread.InvokeAsync(() => MainWindowViewModel.Instance?.CancelModInstall(mod.id), DispatcherPriority.Background);
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.InstallMod()", ex);
                return false;
            }
        }

        public async Task<FsoBuild?> InstallBuild(FsoBuild build, FsoBuildItemViewModel sender, CancellationTokenSource? cancelSource = null, Mod? modJson = null)
        {
            string? modPath = null;
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    if (cancelSource != null)
                    {
                        cancellationTokenSource = cancelSource;
                    }
                    else
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }
                    CancelButtonVisible = true;
                    Name = "Downloading " + build.ToString();
                    ShowProgressText = false;
                    await Dispatcher.UIThread.InvokeAsync(() => TaskRoot.Add(this));
                    Info = "In Queue";

                    //Wait in Queue
                    while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                    {
                        await Task.Delay(1000);
                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }
                    }

                    Info = "Starting";

                    //parse repo to get the data we need
                    if (modJson == null)
                    {
                        modJson = await Nebula.GetModData(build.id, build.version);
                    }

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    if (modJson != null)
                    {
                        /*
                            -Parse all files we need to download
                            -Delete all incompatible packages with system/cpu
                            -Generate the executable properties for valid packages
                            -Add all "ModFile" to a new list for easy access
                            -Create all folders
                            -Create the download token on the root of the mod.
                            -Set all the data needed here, number of tasks, etc for the progress bar and info
                            -Main progress max value is calculated as follows: ( Number of files to download * 2 ) + 2
                             (Download, Decompression, Download banner/tile images)
                        */
                        List<ModFile> files = new List<ModFile>();
                        string modFolder = modJson.id + "-" + modJson.version;
                        modPath = Knossos.GetKnossosLibraryPath() + Path.DirectorySeparatorChar + "bin"+ Path.DirectorySeparatorChar + modFolder;
                        for (int i = modJson.packages.Count - 1; i >= 0; i--)
                        {
                            if (IsEnviromentStringValid(modJson.packages[i].environment))
                            {
                                files.AddRange(modJson.packages[i].files!);
                                foreach (ModExecutable exec in modJson.packages[i].executables!)
                                {
                                    exec.properties = FsoBuild.FillProperties(modJson.packages[i].environment!);
                                }
                            }
                            else
                            {
                                modJson.packages.RemoveAt(i);
                            }
                        }

                        Directory.CreateDirectory(modPath);

                        foreach (var file in files)
                        {
                            if (file.dest != null && file.dest.Trim() != string.Empty)
                            {
                                var path = file.dest;
                                Directory.CreateDirectory(modPath + Path.DirectorySeparatorChar + path);
                            }
                        }

                        ProgressBarMin = 0;
                        sender.ProgressBarCurrent = ProgressCurrent = 0;
                        sender.ProgressBarMax = ProgressBarMax = (files.Count * 2) + 2;
                        Info = "Tasks: 0/" + ProgressBarMax;

                        try
                        {
                            File.Create(modPath + Path.DirectorySeparatorChar + "knossos_net_download.token").Close();
                        }
                        catch { }

                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }

                        /*
                            -Use parallel to process this new list, the max parallelism is the max number of concurrent downloads
                            -Always check canceltask before executing something
                            -Download File -> Verify Checksum -> Extract file
                            -Increase main progress when: 
                             File starts to download, File finishes downloading, Decompression starts, Decompression ends, Image download completed
                        */
                        await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = Knossos.globalSettings.maxConcurrentSubtasks }, async (file, token) =>
                        {
                            if (cancellationTokenSource.IsCancellationRequested)
                            {
                                throw new TaskCanceledException();
                            }

                            //Download
                            var fileTask = new TaskItemViewModel();
                            await Dispatcher.UIThread.InvokeAsync(() => TaskList.Insert(0, fileTask));
                            if (file.dest == null)
                            {
                                file.dest = string.Empty;
                            }
                            
                            Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;
                            var fileFullPath = modPath + Path.DirectorySeparatorChar + file.filename;
                            var result = await fileTask.DownloadFile(file.urls!, fileFullPath, "Downloading " + file.filename, false, null, cancellationTokenSource);
                            
                            if (cancellationTokenSource.IsCancellationRequested)
                            {
                                throw new TaskCanceledException();
                            }

                            if (result.HasValue && result.Value)
                            {
                                sender.ProgressBarCurrent = ++ProgressCurrent;
                                Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;
                            }
                            else
                            {
                                throw new Exception("Error while downloading file: " + fileFullPath);
                            }

                            //Checksum
                            if (file.checksum != null && file.checksum.Count() > 0)
                            {
                                if (file.checksum[0].ToLower() == "sha256")
                                {
                                    using (FileStream? filehash = new FileStream(fileFullPath, FileMode.Open, FileAccess.Read))
                                    {
                                        using (SHA256 checksum = SHA256.Create())
                                        {
                                            filehash.Position = 0;
                                            var hashValue = BitConverter.ToString(await checksum.ComputeHashAsync(filehash)).Replace("-", String.Empty);
                                            filehash.Close();
                                            if (hashValue.ToLower() != file.checksum[1].ToLower())
                                            {
                                                throw new Exception("The downloaded file hash was incorrect, expected: " + file.checksum[1] + " Calculated Hash: " + hashValue);
                                            }
                                        }
                                        fileTask.Info += " Checksum OK!";
                                    }
                                }
                                else
                                {
                                    Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.InstallBuild()", "Unsupported checksum crypto, skipping checksum check :" + file.checksum[0]);
                                }
                            }

                            if (cancellationTokenSource.IsCancellationRequested)
                            {
                                throw new TaskCanceledException();
                            }

                            //Decompress
                            var decompressTask = new TaskItemViewModel();
                            await Dispatcher.UIThread.InvokeAsync(() => TaskList.Insert(0, decompressTask));
                            var decompResult = await decompressTask.DecompressNebulaFile(fileFullPath, file.filename, modPath + Path.DirectorySeparatorChar + file.dest, cancellationTokenSource);
                            if (!decompResult)
                            {
                                Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.InstallBuild()", "Error while decompressing the file " + fileFullPath);
                                CancelTaskCommand();
                            }
                            sender.ProgressBarCurrent = ++ProgressCurrent;
                            File.Delete(fileFullPath);
                        });
                        files.Clear();

                        /*
                            -Delete the download token.
                            -Download tile and banner images, update those file names on json, increase progress
                            -Add folder name and full path to the modJson before creating the fsobuild
                            -Set modJson installed to true before saving json
                            -Serialize json to folder
                            -Create the FsoBuild object and add it to the main list
                            -Return the same FsoObject so it can be updated on the FsoBuildView
                        */

                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }

                        if (!string.IsNullOrEmpty(modJson.tile))
                        {
                            var tileTask = new TaskItemViewModel();
                            await Dispatcher.UIThread.InvokeAsync(() => TaskList.Insert(0, tileTask));
                            await tileTask.DownloadFile(modJson.tile, modPath + Path.DirectorySeparatorChar + "kn_tile.png", "Downloading tile image", false, null, cancellationTokenSource);
                            modJson.tile = "kn_tile.png";
                        }
                        sender.ProgressBarCurrent = ++ProgressCurrent;
                        Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;

                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }

                        if (!string.IsNullOrEmpty(modJson.banner))
                        {
                            var bannerTask = new TaskItemViewModel();
                            await Dispatcher.UIThread.InvokeAsync(() => TaskList.Insert(0, bannerTask));
                            await bannerTask.DownloadFile(modJson.banner, modPath + Path.DirectorySeparatorChar + "kn_banner.png", "Downloading banner image", false, null, cancellationTokenSource);
                            modJson.banner = "kn_banner.png";
                        }

                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }

                        sender.ProgressBarCurrent = ++ProgressCurrent;
                        Info = "Tasks: " + ProgressCurrent + "/" + ProgressBarMax;
                        modJson.fullPath = modPath + Path.DirectorySeparatorChar;
                        modJson.folderName = modFolder;
                        modJson.installed = true;
                        modJson.SaveJson();
                        try
                        {
                            File.Delete(modJson.fullPath + Path.DirectorySeparatorChar + "knossos_net_download.token");
                        }
                        catch (Exception ex)
                        {
                            Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.InstallBuild()", ex);
                        }
                        FsoBuild newBuild = new FsoBuild(modJson);
                        Knossos.AddBuild(newBuild);
                        IsCompleted = true;
                        CancelButtonVisible = false;

                        //Re-run Dependencies checks 
                        MainWindowViewModel.Instance?.RunModStatusChecks();

                        /*
                            Always Dequeue, always check for check size and verify that the first is this TaskItemViewModel object
                        */
                        if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                        {
                            TaskViewModel.Instance!.taskQueue.Dequeue();
                        }
                        /*
                            If flag data wasnt loaded, load it now
                        */
                        if(!Knossos.flagDataLoaded)
                        {
                            MainWindowViewModel.Instance?.GlobalSettingsLoadData();
                        }
                        return newBuild;
                    }
                    else
                    {
                        cancellationTokenSource?.Cancel(); //if some error has ocurred cancel everything
                        Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.InstallBuild()", "Unable to find mod in Nebula repo, requested id:" + build.id + " version: " + build.version);
                        return null;
                    }
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch (TaskCanceledException)
            {
                /*
                    Task cancel requested by user
                */
                IsCompleted = false;
                CancelButtonVisible = false;
                IsCancelled = true;
                Info = "Cancel Requested";
                while(TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                {
                    await Task.Delay(500);
                }
                if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                {
                    TaskViewModel.Instance!.taskQueue.Dequeue();
                }
                await Task.Delay(2000); //give time for child tasks to cancel first
                Info = "Task Cancelled";
                try
                {
                    if (modPath != null)
                    {
                        Directory.Delete(modPath, true);
                    }
                }
                catch { }
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                return null;
            }
            catch (Exception ex)
            {
                /*
                    Task cancel forced due to a error
                */
                IsCompleted = false;
                IsCancelled = true;
                CancelButtonVisible = false;
                cancellationTokenSource?.Cancel();
                Info = "Cancel Requested";
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    //Messagebox is not thread safe!
                    await MessageBox.Show(MainWindow.instance!, "An error was ocurred during the download of the mod: " + build.ToString() + ". Error: " + ex.Message, "Error", MessageBox.MessageBoxButtons.OK);
                });
                while (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() != this)
                {
                    await Task.Delay(500);
                }
                if (TaskViewModel.Instance!.taskQueue.Count > 0 && TaskViewModel.Instance!.taskQueue.Peek() == this)
                {
                    TaskViewModel.Instance!.taskQueue.Dequeue();
                }
                    
                await Task.Delay(2000); //give time for child tasks to cancel first
                Info = "Task Failed";
                try
                {
                    if (modPath != null)
                    {
                        Directory.Delete(modPath, true);
                    }
                }
                catch { }
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.InstallBuild()", ex);
                return null;
            }
        }

        internal void RestartDownloadCommand()
        {
            restartDownload = true;
            if (pauseDownload)
                PauseDownloadCommand();
        }

        internal void PauseDownloadCommand()
        {
            pauseDownload = !pauseDownload;
            if (pauseDownload)
                PauseButtonText = "Resume";
            else
                PauseButtonText = "Pause";
        }

        public async Task<bool?> DownloadFile(string url, string dest, string msg, bool showStopButton, string? tooltip, CancellationTokenSource? cancelSource = null)
        {
            string[] mirrors = { url };
            return await DownloadFile(mirrors, dest, msg, showStopButton, tooltip, cancelSource);
        }

        public async Task<bool?> DownloadFile(string[] mirrors, string dest, string msg, bool showStopButton, string? tooltip, CancellationTokenSource? cancelSource = null)
        {
            try
            {
                if (!TaskIsSet)
                {
                    TaskIsSet = true;
                    ProgressBarMax = 100;
                    ProgressCurrent = 0;
                    IsFileDownloadTask = true;
                    if (cancelSource != null)
                    {
                        cancellationTokenSource = cancelSource;
                    }
                    else
                    {
                        cancellationTokenSource = new CancellationTokenSource();
                    }
                    CancelButtonVisible = showStopButton;
                    Name = msg;
                    if (tooltip != null)
                    {
                        Tooltip = tooltip.Trim();
                        TooltipVisible = true;
                    }

                    var downloadProgress = (long? filesize, long bytesDownloaded, string speed, double? progressPercentage) =>
                    {
                        if (progressPercentage.HasValue && filesize.HasValue)
                        {
                            ProgressCurrent = (float)progressPercentage.Value;
                            Info = SysInfo.FormatBytes(bytesDownloaded) + " / " + SysInfo.FormatBytes(filesize.Value) + " @ " + speed ;
                        }

                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }
                        return restartDownload;
                    };

                    bool result = false;

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    result = await Download(mirrors, dest, downloadProgress);

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }
                    CancelButtonVisible = false;
                    if (result)
                    {
                        IsCompleted = true;
                        return true;
                    }
                    else
                    {
                        IsCompleted = false;
                        return false;
                    }
                }
                else
                {
                    throw new Exception("The task is already set, it cant be changed or re-assigned.");
                }
            }
            catch (TaskCanceledException)
            {
                /*
                    Task cancel requested by user
                */
                IsCompleted = false;
                IsCancelled = true;
                CancelButtonVisible = false;
                Info = "Task Cancelled";
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                return false;
            }
            catch (Exception ex)
            {
                IsCompleted = false;
                CancelButtonVisible = false;
                IsCancelled = true;
                Info = "Task Failed";
                //Only dispose the token if it was created locally
                if (cancelSource == null)
                {
                    cancellationTokenSource?.Dispose();
                }
                Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.DownloadFile()", ex);
                return false;
            }
        }

        public void CancelTaskCommand()
        {
            if (!IsCompleted)
            {
                cancellationTokenSource?.Cancel();
                IsCancelled = true;
            }
        }


        private bool IsEnviromentStringValid(string? enviroment)
        {
            if(enviroment == null || enviroment.Trim() == string.Empty)
            { 
                return false; 
            }

            if(enviroment.ToLower().Contains("windows") && !SysInfo.IsWindows || enviroment.ToLower().Contains("linux") && !SysInfo.IsLinux || enviroment.ToLower().Contains("macosx") && !SysInfo.IsMacOS)
            {
                return false;
            }

            if (enviroment.ToLower().Contains("avx2") && !SysInfo.CpuAVX2 || enviroment.ToLower().Contains("avx") && !SysInfo.CpuAVX)
            {
                return false;
            }

            if (enviroment.ToLower().Contains("x86_64") && SysInfo.CpuArch == "X64")
            {
                return true;
            }
            if (enviroment.ToLower().Contains("arm64") && SysInfo.CpuArch == "Arm64")
            {
                return true;
            }
            if (enviroment.ToLower().Contains("arm32") && (SysInfo.CpuArch == "Armv6" || SysInfo.CpuArch == "Arm"))
            {
                return true;
            }
            if(SysInfo.CpuArch == "X86" && !enviroment.ToLower().Contains("x86_64") && !enviroment.ToLower().Contains("arm64") && !enviroment.ToLower().Contains("arm32"))
            {
                return true;
            }
            if (SysInfo.CpuArch == "X64" && !enviroment.ToLower().Contains("x86_64") && !enviroment.ToLower().Contains("arm64") && !enviroment.ToLower().Contains("arm32"))
            {
                return true;
            }

            if (SysInfo.CpuArch == "Arm64" && (SysInfo.IsMacOS || SysInfo.IsWindows) && !enviroment.ToLower().Contains("arm32") && !enviroment.ToLower().Contains("avx"))
            {
                return true;
            }

            return false;
        }

        private async Task<bool> Download(string[] downloadMirrors, string destinationFilePath, Func<long?, long, string, double?, bool> progressChanged)
        {
            Random rnd = new Random();
            int maxRetries = 15;
            int count = 0;
            bool result = false;
            IsFileDownloadTask = true;
            int lastMirrorIndex = -1;
            do
            {
                if(restartDownload)
                {
                    restartDownload = false;
                }
                else
                {
                    count++;
                }
                var mirrorIndex = rnd.Next(downloadMirrors.Count());
                Uri uri = new Uri(downloadMirrors[mirrorIndex]);
                
                while (downloadMirrors.Count() > 1 && ( (mirrorIndex == lastMirrorIndex && (Knossos.globalSettings.mirrorBlacklist == null || downloadMirrors.Count() - Knossos.globalSettings.mirrorBlacklist.Count() > 1 )) || (Knossos.globalSettings.mirrorBlacklist != null && Knossos.globalSettings.mirrorBlacklist.Contains(uri.Host)) ) )
                {
                    mirrorIndex = rnd.Next(downloadMirrors.Count());
                    uri = new Uri(downloadMirrors[mirrorIndex]);
                }

                CurrentMirror = uri.Host;
                lastMirrorIndex = mirrorIndex;

                if (count > 1)
                {
                    Log.Add(Log.LogSeverity.Warning, "TaskItemViewModel.Download(List<mirrors>)", "Retrying download of file: " + uri.ToString());
                }
                result = await Download(uri, destinationFilePath, progressChanged);
                if (cancellationTokenSource!.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }
            } while (result != true && count < maxRetries);
            
            return result;
        }

        private async Task<bool> Download(Uri downloadUrl, string destinationFilePath, Func<long?, long, string, double?, bool> progressChanged)
        {
            try
            {
                bool isJson = false;
                Log.Add(Log.LogSeverity.Information, "TaskItemViewModel.Download()", "Downloading file: " + downloadUrl);
                System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                HttpClientHandler handler = new HttpClientHandler();
                handler.AllowAutoRedirect = true;
                handler.AutomaticDecompression = DecompressionMethods.All;
                using HttpClient httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                if (downloadUrl.ToString().ToLower().Contains(".json"))
                {
                    httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "br, gzip, deflate");
                    isJson = true;
                }
                using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;

                if (!totalBytes.HasValue)
                {
                    foreach (string s in response.Headers.Vary)
                    {
                        if (s == "Accept-Encoding")
                        {
                            var c = new HttpClient();
                            c.Timeout = TimeSpan.FromSeconds(10);
                            var r = await c.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                            totalBytes = r.Content.Headers.ContentLength;
                            r.Dispose(); c.Dispose();
                            continue;
                        }
                    }
                }

                using var contentStream = Knossos.globalSettings.maxDownloadSpeed > 0 && !isJson ? new ThrottledStream(response.Content.ReadAsStream(), Knossos.globalSettings.maxDownloadSpeed) : response.Content.ReadAsStream();
                var totalBytesRead = 0L;
                var totalBytesPerSecond = 0L;
                var readCount = 0L;
                var buffer = new byte[262144];
                var isMoreToRead = true;
                var speed = string.Empty;

                static double? calculatePercentage(long? totalDownloadSize, long totalBytesRead) => totalDownloadSize.HasValue ? Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 2) : null;

                using var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 262144, true);
                stopwatch.Start();
                do
                {
                    while(pauseDownload && !restartDownload)
                    {
                        await Task.Delay(500);
                        if (cancellationTokenSource!.IsCancellationRequested)
                        {
                            throw new TaskCanceledException();
                        }
                    }

                    if (cancellationTokenSource!.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    var bytesRead = await contentStream.ReadAsync(buffer);
                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;

                        if (progressChanged(totalBytes, totalBytesRead, string.Empty, calculatePercentage(totalBytes, totalBytesRead)))
                        {
                            stopwatch.Reset();
                            throw new OperationCanceledException();
                        }

                        continue;
                    }

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));

                    totalBytesRead += bytesRead;
                    totalBytesPerSecond += bytesRead;
                    readCount++;

                    if (stopwatch.Elapsed.TotalSeconds >= 1)
                    {
                        speed = SysInfo.FormatBytes(totalBytesPerSecond)+"/s";
                        totalBytesPerSecond = 0L;
                        stopwatch.Restart();
                    }


                    if (readCount % 100 == 0)
                    {
                        if (progressChanged(totalBytes, totalBytesRead, speed, calculatePercentage(totalBytes, totalBytesRead)))
                        {
                            stopwatch.Reset();
                            throw new OperationCanceledException();
                        }
                    }
                }
                while (isMoreToRead);
                stopwatch.Reset();
                return true;
            }
            catch(Exception ex)
            {
                Log.Add(Log.LogSeverity.Error, "TaskItemViewModel.Download", ex);
                return false;
            }
        }
    }
}
