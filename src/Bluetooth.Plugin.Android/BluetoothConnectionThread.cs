using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.IO;
using Java.Lang;
using Plugin.Bluetooth.Abstractions.Exceptions;
using Exception = System.Exception;
using Java.Util;
using Plugin.Bluetooth.Abstractions.Args;

namespace Bluetooth.Plugin.Android
{
    public class BluetoothConnectionThread : Thread
    {
        volatile bool running = true;
        public BluetoothSocket Socket;
        private BluetoothDevice m_device;
        private BluetoothAdapter m_bluetoothAdapter;
        private int m_port = 1;

        public BluetoothConnectionThread(BluetoothDevice device)
        {
            // Use a temporary object that is later assigned to mmSocket,
            // because mmSocket is final
            BluetoothSocket tmp = null;
            m_device = device;
            m_bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
            if (m_bluetoothAdapter == null)
            {
                // Device does not support Bluetooth
                throw new System.Exception("No bluetooth device found");
            }
        }

        public override void Run()
        {
            if (IsInterrupted)
                return;

            try
            {
                ProbeConnection();
            }
            catch (IOException connectException)
            {
                // Unable to connect; close the socket and get out
                try
                {
                    Socket.Close();
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Faield to close socket, {ex}");
                }

                throw new BluetoothDeviceNotFoundException(connectException.Message);
            }

        }

        private void ProbeConnection()
        {
            ParcelUuid[] parcelUuids;
            parcelUuids = m_device.GetUuids();
            bool isConnected = false;

            if (parcelUuids == null)
            {
                parcelUuids = new ParcelUuid[1];
                parcelUuids[0] = new ParcelUuid(UUID.FromString("00001101-0000-1000-8000-00805f9b34fb"));
            }

            m_bluetoothAdapter.CancelDiscovery();


            //METHOD A

            try
            {
                var method = m_device.GetType().GetMethod("createRfcommSocket");
                Socket = (BluetoothSocket)method.Invoke(m_device, new object[] { m_port });
                Socket.Connect();
                isConnected = true;
                DoDeviceConnected();

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Faield to createRfcommSocket {ex}");
            }

            if (!isConnected)
            {
                //METHOD B

                try
                {
                    var method = m_device.GetType().GetMethod("createInsecureRfcommSocket");
                    Socket = (BluetoothSocket)method.Invoke(m_device, new object[] { m_port });
                    Socket.Connect();
                    isConnected = true;
                    DoDeviceConnected();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Faield to createInsecureRfcommSocket {ex}");
                }
            }

            if (!isConnected)
            {
                //METHOD C

                try
                {
                    IntPtr createRfcommSocket = JNIEnv.GetMethodID(m_device.Class.Handle, "createRfcommSocket", "(I)Landroid/bluetooth/BluetoothSocket;");
                    IntPtr _socket = JNIEnv.CallObjectMethod(m_device.Handle, createRfcommSocket, new global::Android.Runtime.JValue(m_port));
                    Socket = Java.Lang.Object.GetObject<BluetoothSocket>(_socket, JniHandleOwnership.TransferLocalRef);
                    Socket.Connect();
                    isConnected = true;
                    DoDeviceConnected();
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Faield to createRfcommSocket JNI, {ex}");
                }
            }

            if (!isConnected)
            {
                // Unable to connect; close the socket and get out
                if (Socket != null)
                {
                    try
                    {
                        Socket.Close();
                    }
                    catch (IOException e)
                    {
                        System.Diagnostics.Debug.WriteLine($"Faield to close socket, {e}");
                    }
                }
                DoDeviceConnectionFailed();
            }
        }

        public event EventHandler DeviceConnected;
        private void DoDeviceConnected()
        {
            System.Diagnostics.Debug.WriteLine("device connected");
            DeviceConnected?.Invoke(this, new EventArgs());
        }

        public event EventHandler DeviceConnectionFailed;
        private void DoDeviceConnectionFailed()
        {
            System.Diagnostics.Debug.WriteLine("device connection failed");
            DeviceConnectionFailed?.Invoke(this, new EventArgs());
        }

        public event EventHandler<BluetoothDataReceivedEventArgs> ReceivedData;
        private void DoReceivedData(byte[] data)
        {
            ReceivedData?.Invoke(this, new BluetoothDataReceivedEventArgs(data));
        }

        /** Will cancel an in-progress connection, and close the socket */
        public void Disconnect()
        {
            try
            {
                Socket.Close();
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Disconnect() faild to close socket {ex}");
            }
            finally
            {
                Socket = null;
                this.Interrupt();
            }
        }

        //Method obtained from
        //https://stackoverflow.com/questions/44593085/android-bluetooth-how-to-read-incoming-data
        private async void ReceiveData(BluetoothSocket socket)
        {
            var socketInputStream = socket.InputStream;
            byte[] buffer = new byte[256];
            int bytes;

            // Keep looping to listen for received messages
            while (!IsInterrupted)
            {
                byte[] result;
                int length;
                length = (int)socketInputStream.Length;
                result = new byte[length];
                await socketInputStream.ReadAsync(result, 0, length);
                if (length > 0)
                    DoReceivedData(result);
            }
        }
    }
}