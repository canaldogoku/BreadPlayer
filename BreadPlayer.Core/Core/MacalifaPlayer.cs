﻿/* 
	BreadPlayer. A music player made for Windows 10 store.
    Copyright (C) 2016  theweavrs (Abdullah Atta)

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Media.Render;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ManagedBass;
using System.Runtime.InteropServices;
using BreadPlayer.Events;
using System.Diagnostics;
using BreadPlayer.Models;

namespace BreadPlayer.Core
{
    public class MacalifaPlayer : ViewModelBase, IDisposable
    {
        #region Fields
        public int handle = 0;
        private SyncProcedure _sync;
        #endregion

        #region Constructor
        public MacalifaPlayer()
        {
            Init();
            _sync = new SyncProcedure(EndSync);
        }
        #endregion
       
        #region Initialize Methods
            /// <summary>
            /// Initializes the player to start playing audio
            /// </summary>
            /// <returns></returns>
        private void Init()
        {
            Bass.UpdatePeriod = 10;
            Bass.Start();
            Bass.Init();
        }
        private void InitializeExtensions(Mediafile file)
        {
            //Effect = new Effects(handle);
        }
        #endregion

        #region Methods
        /// <summary>
        /// Swipes the player memory clean.
        /// </summary>
        /// <returns></returns>
        public async void Dispose()
        {
            await Task.Run(() =>
            {
                Bass.ChannelStop(handle); // Stop Playback.
                Bass.MusicFree(handle); // Free the Stream.
                Bass.Free(); // Frees everything (will have to call init again to play audio)
                handle = 0;
            });
        }
        /// <summary>
        /// Loads the specified file into the player.
        /// </summary>
        /// <param name="fileName">Path to the music file.</param>
        /// <returns>Boolean</returns>
        public async Task<bool> Load(Mediafile mp3file)
        {         
            if (mp3file != null)
            {
                string sPath = mp3file.Path;
                await Stop();
                await Task.Run(() =>
                {
                    handle = ManagedBass.Bass.CreateStream(sPath, 0, 0, BassFlags.AutoFree | BassFlags.Float);
                    PlayerState = PlayerState.Stopped;
                    Length = Bass.ChannelBytes2Seconds(handle, Bass.ChannelGetLength(handle));
                    MediaStateChanged(this, new MediaStateChangedEventArgs(PlayerState.Stopped));
                    Bass.ChannelSetSync(handle, SyncFlags.End | SyncFlags.Mixtime, 0, _sync);
                    CurrentlyPlayingFile = mp3file;
                    CoreWindowLogic.UpdateSmtc();
                    CoreWindowLogic.Stringify();
                });
                Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.Remove(CoreMethods.CurrentFileToken);
                return true;
            }
            else
                return false;
        }
        /// <summary>
        /// Pauses the audio playback.
        /// </summary>
        /// <returns></returns>
        public async Task Pause()
        {
            await Task.Run(() =>
            {
                Bass.ChannelPause(handle);
                PlayerState = PlayerState.Paused;
                MediaStateChanged(this, new MediaStateChangedEventArgs(PlayerState.Paused));
            });

        }
        /// <summary>
        /// Starts the audio playback.
        /// </summary>
        /// <returns></returns>
        public async Task Play()
        {
            await Task.Run(() =>
            {
                ManagedBass.Bass.ChannelPlay(handle);
                PlayerState = PlayerState.Playing;
                MediaStateChanged(this, new MediaStateChangedEventArgs(PlayerState.Playing));
             
            });

        }
        /// <summary>
        /// Stops the playback if it is playing.
        /// </summary>
        /// <returns></returns>
        public async Task Stop()
        {
            await Task.Run(() =>
            {
                if (handle != 0)
                {
                    Bass.ChannelStop(handle); // Stop Playback.
                    Bass.MusicFree(handle);
                    PlayerState = PlayerState.Stopped;
                    MediaStateChanged(this, new MediaStateChangedEventArgs(PlayerState.Stopped));
                }
            });

        }
        #endregion

        #region Properties
        double _volume = 50;
        public double Volume
        {
            get { return _volume; }
            set {
                Set(ref _volume, value);
                    Bass.Volume = _volume / 100;
            }
        }
        public Effects Effect
        {
            get; set;
        }

        double _seek = 0;
        public double Position
        {
            get { return Bass.ChannelBytes2Seconds(handle, Bass.ChannelGetPosition(handle)); }
            set
            {
                Task.Run(() =>
                {
                    Set(ref _seek, value);
                    Bass.ChannelSetPosition(handle, ManagedBass.Bass.ChannelSeconds2Bytes(handle, _seek));
                });
            }            
        }
        double _length;
        public double Length
        {
            get {
                return _length;
            }
            set
            {
                Set(ref _length, value);
            }
        }      

        public PlayerState PlayerState
        {
            get; set;
        }
        Mediafile _currentPlayingFile;
        public Mediafile CurrentlyPlayingFile
        {
            get { return _currentPlayingFile; }
            set { Set(ref _currentPlayingFile, value); }
        }
        #endregion

        private void EndSync(int handle, int channel, int data, IntPtr user)
        {
            MediaEnded(this, new MediaEndedEventArgs(PlayerState.Ended));
        }
        public event OnMediaStateChanged MediaStateChanged;
        public event OnMediaEnded MediaEnded;
    }

    public delegate void OnMediaStateChanged(object sender, MediaStateChangedEventArgs e);
    public delegate void OnMediaEnded(object sender, MediaEndedEventArgs e);
}
