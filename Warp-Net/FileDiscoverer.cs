﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Warp.Tools;

namespace Warp
{
    public class FileDiscoverer
    {
        int ExceptionsLogged = 0;

        List<Tuple<string, Stopwatch, long>> Incubator = new List<Tuple<string, Stopwatch, long>>();    // Files that could still change in size go here.
        string FolderPath = "";
        string FileExtension = "*.*";
        BackgroundWorker DiscoveryThread;
        bool ShouldAbort = false;

        FileSystemWatcher FileWatcher = null;
        bool FileWatcherRaised = false;

        List<Movie> Ripe = new List<Movie>();   // Files that haven't changed in size for 1000 ms go here.

        public event Action IncubationStarted;
        public event Action IncubationEnded;
        public event Action FilesChanged;

        Dictionary<string, Task> CreationTasks = new Dictionary<string, Task>();

        public FileDiscoverer()
        {
            FileWatcher = new FileSystemWatcher();
            FileWatcher.IncludeSubdirectories = false;
            FileWatcher.Changed += FileWatcher_Changed;
            FileWatcher.Created += FileWatcher_Changed;
            FileWatcher.Renamed += FileWatcher_Changed;
        }

        private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            FileWatcherRaised = true;
        }

        public void ChangePath(string newPath, string newExtension)
        {
            IncubationEnded?.Invoke();

            //if (newExtension == "*.tif")
            //    newExtension = "*.tif*";

            lock (Incubator)
            {
                if (DiscoveryThread != null && DiscoveryThread.IsBusy)
                {
                    DiscoveryThread.RunWorkerCompleted += (sender, args) =>
                    {
                        FileWatcher.EnableRaisingEvents = false;
                        FileWatcherRaised = false;
                        ShouldAbort = false;

                        FolderPath = newPath;
                        FileExtension = newExtension;

                        if (FolderPath == "" || !Directory.Exists(FolderPath) || !IOHelper.CheckFolderPermission(FolderPath))
                            return;

                        Task.WaitAll(CreationTasks.Values.ToArray());
                        //Thread.Sleep(500);  // There might still be item creation tasks running asynchro

                        Incubator.Clear();
                        lock (Ripe)
                            Ripe.Clear();

                        FilesChanged?.Invoke();

                        FileWatcher.Path = FolderPath;
                        FileWatcher.Filter = FileExtension;
                        FileWatcher.EnableRaisingEvents = true;

                        DiscoveryThread = new BackgroundWorker();
                        DiscoveryThread.DoWork += WorkLoop;
                        DiscoveryThread.RunWorkerAsync();
                    };

                    ShouldAbort = true;
                }
                else
                {
                    FolderPath = newPath;
                    FileExtension = newExtension;

                    if (FolderPath == "" || !Directory.Exists(FolderPath) || !IOHelper.CheckFolderPermission(FolderPath))
                        return;

                    Incubator.Clear();
                    lock (Ripe)
                        Ripe.Clear();

                    FilesChanged?.Invoke();

                    FileWatcher.Path = FolderPath;
                    FileWatcher.Filter = FileExtension;
                    FileWatcher.EnableRaisingEvents = true;

                    DiscoveryThread = new BackgroundWorker();
                    DiscoveryThread.DoWork += WorkLoop;
                    DiscoveryThread.RunWorkerAsync();
                }
            }
        }

        void WorkLoop(object sender, EventArgs e)
        {
            string[] ParticleFileNames = null;
            if (Directory.Exists(Path.Combine(FolderPath, "matching")))
                ParticleFileNames = Directory.EnumerateFiles(Path.Combine(FolderPath, "matching"), "*.star", SearchOption.TopDirectoryOnly).Select(p => Helper.PathToNameWithExtension(p)).ToArray();

            while (true)
            {
                if (ShouldAbort)
                    return;

                Stopwatch Watch = new Stopwatch();
                bool EventNeedsFiring = false;
                FileWatcherRaised = false;

                try
                {
                    foreach (var fileName in Directory.EnumerateFiles(FolderPath, FileExtension, SearchOption.TopDirectoryOnly).ToArray())
                    {
                        if (ShouldAbort)
                            return;

                        if (GetMovie(fileName) != null)
                            continue;

                        string NameXML = fileName.Substring(0, fileName.LastIndexOf(".")) + ".xml";

                        if (!File.Exists(NameXML))
                        {
                            FileInfo Info = new FileInfo(fileName);
                            Tuple<string, Stopwatch, long> CurrentState = GetIncubating(fileName);
                            if (CurrentState == null)
                            {
                                Stopwatch Timer = new Stopwatch();
                                Timer.Start();
                                lock (Incubator)
                                {
                                    Incubator.Add(new Tuple<string, Stopwatch, long>(fileName, Timer, Info.Length));
                                    if (Incubator.Count == 1)
                                        IncubationStarted?.Invoke();
                                }
                            }
                            else
                            {
                                // Check if 
                                bool CanRead = false;
                                try
                                {
                                    File.OpenRead(fileName).Close();
                                    CanRead = true;
                                }
                                catch
                                {
                                }

                                if (Info.Length != CurrentState.Item3 || !CanRead)
                                {
                                    lock (Incubator)
                                        Incubator.Remove(CurrentState);
                                    Stopwatch Timer = new Stopwatch();
                                    Timer.Start();
                                    lock (Incubator)
                                        Incubator.Add(new Tuple<string, Stopwatch, long>(fileName, Timer, Info.Length));
                                }
                                else if (CurrentState.Item2.ElapsedMilliseconds > 1000)
                                {
                                    //lock (Ripe)
                                    {
                                        if (!Ripe.Exists(m => m.Path == fileName))
                                        {
                                            while (CreationTasks.Count > 15)
                                                Thread.Sleep(1);

                                            Task CreationTask = new Task(() =>
                                            {
                                                Movie Created = null;
                                                if (!fileName.Substring(fileName.Length - 8).ToLower().Contains("tomostar"))
                                                    Created = new Movie(fileName, ParticleFileNames);
                                                else
                                                    Created = new TiltSeries(fileName);

                                                lock (Ripe)
                                                {
                                                    // Make sure the list is sorted
                                                    int InsertAt = 0;
                                                    while (InsertAt < Ripe.Count && Ripe[InsertAt].Path.CompareTo(fileName) < 0)
                                                        InsertAt++;

                                                    Ripe.Insert(InsertAt, Created);
                                                }

                                                lock (CreationTasks)
                                                    CreationTasks.Remove(fileName);
                                            });

                                            lock (CreationTasks)
                                                CreationTasks.Add(fileName, CreationTask);

                                            CreationTask.Start();
                                        }
                                    }

                                    EventNeedsFiring = true;
                                    if (!Watch.IsRunning)
                                        Watch.Start();

                                    lock (Incubator)
                                    {
                                        Incubator.Remove(CurrentState);
                                        if (Incubator.Count == 0)
                                            IncubationEnded?.Invoke();
                                    }
                                }
                            }

                            if (EventNeedsFiring && Watch.ElapsedMilliseconds > 500)
                            {
                                //Task.WaitAll(CreationTasks.ToArray());
                                //CreationTasks.Clear();

                                Watch.Stop();
                                Watch.Reset();
                                EventNeedsFiring = false;

                                FilesChanged?.Invoke();
                            }
                        }
                        else
                        {
                            //lock (Ripe)
                            {
                                if (!Ripe.Exists(m => m.Path == fileName))
                                {
                                    while (CreationTasks.Count > 15)
                                        Thread.Sleep(1);

                                    Task CreationTask = new Task(() =>
                                    {
                                        Movie Created = null;
                                        if (!fileName.Substring(fileName.Length - 8).ToLower().Contains("tomostar"))
                                            Created = new Movie(fileName, ParticleFileNames);
                                        else
                                            Created = new TiltSeries(fileName);

                                        lock (Ripe)
                                        {
                                            // Make sure the list is sorted
                                            int InsertAt = 0;
                                            while (InsertAt < Ripe.Count && Ripe[InsertAt].Path.CompareTo(fileName) < 0)
                                                InsertAt++;

                                            Ripe.Insert(InsertAt, Created);
                                        }

                                        lock (CreationTasks)
                                            CreationTasks.Remove(fileName);
                                    });

                                    lock (CreationTasks)
                                        CreationTasks.Add(fileName, CreationTask);

                                    CreationTask.Start();
                                }
                            }

                            EventNeedsFiring = true;
                            if (!Watch.IsRunning)
                                Watch.Start();

                            if (EventNeedsFiring && Watch.ElapsedMilliseconds > 500)
                            {
                                //Task.WaitAll(CreationTasks.ToArray());
                                //CreationTasks.Clear();

                                Watch.Stop();
                                Watch.Reset();
                                EventNeedsFiring = false;

                                FilesChanged?.Invoke();
                            }
                        }
                    }
                }
                catch (Exception exc)
                {
                    Debug.WriteLine("FileDiscoverer crashed:");
                    Debug.WriteLine(exc);

                    if (ExceptionsLogged < 100)
                        try
                        {
                            using (TextWriter Writer = File.AppendText("d_filediscoverer.txt"))
                            {
                                Writer.WriteLine(DateTime.Now + ":");
                                Writer.WriteLine(exc.ToString());
                                Writer.WriteLine("");
                                ExceptionsLogged++;
                            }
                        }
                        catch { }
                }

                while (CreationTasks.Count > 0)
                    Thread.Sleep(1);

                if (EventNeedsFiring)
                    FilesChanged?.Invoke();

                while (!IsIncubating() && !FileWatcherRaised)
                {
                    if (ShouldAbort)
                        return;
                    Thread.Sleep(50);
                }
            }
        }

        public void Shutdown()
        {
            lock (Incubator)
                ShouldAbort = true;
        }

        Tuple<string, Stopwatch, long> GetIncubating(string path)
        {
            lock (Incubator)
                return Incubator.FirstOrDefault(tuple => tuple.Item1 == path);
        }

        Movie GetMovie(string path)
        {
            lock (Ripe)
                return Ripe.FirstOrDefault(movie => movie.Path == path);
        }

        public Movie[] GetImmutableFiles()
        {
            lock (Ripe)
                return Ripe.ToArray();
        }

        public bool IsIncubating()
        {
            return Incubator.Count > 0;
        }
    }
}
