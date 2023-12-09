using EzASD.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EzASD
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        internal RecordsContext DbContext { get; }

        public App()
        {
            DbContext = new RecordsContext();
            InitializeComponent();
            Exit += (sender, e) =>
            {
                DbContext.SaveChanges();
                DbContext.Dispose();
            };
        }

    }
}
