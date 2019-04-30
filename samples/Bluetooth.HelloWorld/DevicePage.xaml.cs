using Plugin.Bluetooth.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace Bluetooth.HelloWorld
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class DevicePage : ContentPage
    {
        private IBluetoothDevice _bluetoothDevice;
        private Timer m_timer = new Timer();
        public DevicePage(IBluetoothDevice bluetoothDevice)
        {
            InitializeComponent();
            m_timer.Interval = 1000;
            m_timer.Elapsed += async (sender, e) =>
            {
                try
                {
                    if (!_bluetoothDevice.IsConnected)
                        throw new Exception("not connected");

                    await _bluetoothDevice.Write("test");
                }
                catch (Exception ex)
                {
                    m_timer.Stop();
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Error", $"failed to write to device, {ex}", "Ok");
                        await Navigation.PopAsync();
                    });

                }
            };
            _bluetoothDevice = bluetoothDevice;
            this.Appearing += DevicePage_Appearing;
        }

        private async void DevicePage_Appearing(object sender, EventArgs e)
        {
            try
            {
                if (!_bluetoothDevice.IsConnected)
                    await _bluetoothDevice.Connect();

                m_timer.Start();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Couldnt connect to device, {ex}", "Ok");
                await Navigation.PopAsync();
            }
        }

        protected override void OnDisappearing()
        {
            try
            {
                m_timer.Stop();
                if (_bluetoothDevice.IsConnected)
                    _bluetoothDevice.Disconnect().Wait();
            }
            catch
            {
                //TODO
            }
            base.OnDisappearing();
        }

    }
}