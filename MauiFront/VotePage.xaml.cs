using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text.Json;
using Syncfusion.Maui.Scheduler;


namespace MauiFront
{
    
    public partial class VotePage : ContentPage
    {
        public ObservableCollection<SchedulerAppointment> EventosAgendados { get; set; }

        public VotePage()
        {
            InitializeComponent();

        }

      
        
    }
}