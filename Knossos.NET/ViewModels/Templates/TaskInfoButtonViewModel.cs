﻿using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Knossos.NET.ViewModels
{
    public partial class TaskInfoButtonViewModel : ViewModelBase
    {
        internal TaskViewModel? TaskViewModel { get; set; }
        [ObservableProperty]
        internal int taskNumber = 0;
        [ObservableProperty]
        internal string tooltip = "";
        [ObservableProperty]
        internal bool frame0 = true;
        [ObservableProperty]
        internal bool frame1 = false;

        public TaskInfoButtonViewModel() 
        {
            TaskNumber = 0;
        }

        public TaskInfoButtonViewModel(TaskViewModel taskViewModel)
        {
            TaskViewModel = taskViewModel;
            TaskNumber = taskViewModel.NumberOfTasks();
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Interval = 1500;
            timer.Elapsed += Update;
            timer.Start();
        }

        internal void ToggleTaskView()
        {
            TaskViewModel?.ToggleCommand();
        }

        private void Animate()
        {
            if(Frame0)
            {
                Frame0 = false;
                Frame1 = true;
            }
            else
            {
                Frame1 = false;
                Frame0 = true;
            }
        }

        private void Update(object? _, System.Timers.ElapsedEventArgs __)
        {
            if(TaskViewModel != null)
            {
                int tasks = TaskViewModel.NumberOfTasks();
                TaskNumber = tasks;
                
                if (tasks > 0)
                {
                    Tooltip = "Open Task List\n\n" + TaskViewModel.GetRunningTaskString();
                    if (!TaskViewModel.IsSafeState())
                    {
                        Animate();
                    }
                }
                else
                {
                    Tooltip = "Open Task List";
                }
            }
        }
    }
}
