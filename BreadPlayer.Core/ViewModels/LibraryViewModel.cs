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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
using System.IO;
using Windows.Storage;
using Windows.Storage.Pickers;
using BreadPlayer.Tags;
using BreadPlayer.Tags.ID3;
using BreadPlayer.Tags.ID3.ID3v2Frames;
using BreadPlayer.Tags.ID3.ID3v2Frames.TextFrames;
using Windows.UI.Xaml.Media;
using BreadPlayer.Models;
using System.Collections.ObjectModel;
using BreadPlayer.Core;
using BreadPlayer.Services;
using System.Windows.Input;
using System.Reflection;
using Windows.Data.Json;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using BreadPlayer.Database;
using Windows.UI.Xaml.Controls;
using BreadPlayer.Extensions;
using Windows.UI.Xaml.Data;
using System.Diagnostics;
using Windows.System;
using BreadPlayer.Events;
using BreadPlayer.Dialogs;
using System.Security.Cryptography;
using SplitViewMenu;
using Windows.Storage.AccessCache;
using BreadPlayer.Tags.ID3.ID3v2Frames.BinaryFrames;

namespace BreadPlayer.ViewModels
{
    public class LibraryViewModel : ViewModelBase
    {
        #region Fields
        public QueryMethods db = new QueryMethods();
        public CoreDispatcher Dispatcher;
        ThreadSafeObservableCollection<Playlist> PlaylistCollection = new ThreadSafeObservableCollection<Playlist>();
        ObservableRangeCollection<String> _GenreCollection = new ObservableRangeCollection<string>();
        public IEnumerable<Mediafile> OldItems;
        public ListBox FileListBox;
        public static string Path = "";
        #endregion

        #region Contructor
        /// <summary>
        /// Creates a new instance of LibraryViewModel
        /// </summary>
        public LibraryViewModel()
        {
            MusicLibraryLoaded += LibraryViewModel_MusicLibraryLoaded;
            GenericService<LibraryViewModel>.vm = this;
            GenericService<LibraryViewModel> service = GenericService<LibraryViewModel>.Instance;
            Dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
            RecentlyPlayedCollection.Elements.CollectionChanged += Elements_CollectionChanged;
            LoadLibrary();
            //SplitViewMenu.SplitViewMenu.NavService.Frame.Navigated += Frame_Navigated;
        }

        private async void Elements_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            await Task.Delay(1000);
           if(RecentlyPlayedCollection.Elements.Count <= 100)
            {              
                RecentlyPlayedCollection.Elements.RemoveAt(RecentlyPlayedCollection.Count + 1);
            }
        }

        private void Frame_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.SourcePageType == typeof(LibraryView))
            {
                if (e.Parameter is string && e.Parameter.ToString() == "Recent")
                {
                    ViewSource.Source = RecentlyPlayedCollection.Elements;
                    //headerText.Text = "Recently Played";
                }
                else
                {
                    ViewSource.Source = TracksCollection.Elements;
                }
            }

        }
        #endregion

        #region Properties  
        public ThreadSafeObservableCollection<ContextMenuCommand> Options { get; set; } = new ThreadSafeObservableCollection<ContextMenuCommand>();

        CollectionViewSource viewSource;
        public CollectionViewSource ViewSource
        {
            get { if (viewSource == null) viewSource = new CollectionViewSource(); return viewSource; }
            set { Set(ref viewSource, value); }
        }
        string _genre;
        public string Genre
        {
            get { return _genre; }
            set { Set(ref _genre, value); }
        }
        /// <summary>
        /// Gets the genre collection in which there are all the genres of all the tracks/Mediafiles in <see cref="FileCollection"/>.
        /// </summary>
        public ObservableRangeCollection<String> GenreCollection
        {
            get { return _GenreCollection; }
        }
        GroupedObservableCollection<string, Mediafile> _RecentlyPlayedCollection;
        /// <summary>
        /// Gets or sets a grouped observable collection of Tracks/Mediafiles. <seealso cref="GroupedObservableCollection{TKey, TElement}"/>
        /// </summary>
        public GroupedObservableCollection<string, Mediafile> RecentlyPlayedCollection
        {
            get { if (_RecentlyPlayedCollection == null) _RecentlyPlayedCollection = new GroupedObservableCollection<string, Mediafile>(t => t.Title); return _RecentlyPlayedCollection; }
            set { Set(ref _RecentlyPlayedCollection, value); }
        }
        GroupedObservableCollection<string, Mediafile> _TracksCollection;
        /// <summary>
        /// Gets or sets a grouped observable collection of Tracks/Mediafiles. <seealso cref="GroupedObservableCollection{TKey, TElement}"/>
        /// </summary>
        public GroupedObservableCollection<string, Mediafile> TracksCollection
        {
            get { if (_TracksCollection == null) _TracksCollection = new GroupedObservableCollection<string, Mediafile>(t => t.Title); return _TracksCollection; }
            set { Set(ref _TracksCollection, value); }
        }
        public MenuFlyout genreFlyout;
        /// <summary>
        /// Gets or Sets a flyout for genres. This is a dynamic control bound to <see cref="LibraryView"/>.
        /// </summary>
        public MenuFlyout GenreFlyout
        {
            get { return genreFlyout; }
            set { Set(ref genreFlyout, value); }
        }
        Mediafile _mediaFile;
        /// <summary>
        /// Gets or Sets <see cref="BreadPlayer.Models.Mediafile"/> for this ViewModel
        /// </summary>
        public Mediafile Mediafile
        {
            get { return _mediaFile; }
            set { Set(ref _mediaFile, value); }
        }
        #endregion

        #region Commands 

        #region Definitions
        RelayCommand _showPropertiesCommand;
        RelayCommand _deleteCommand;
        RelayCommand _playCommand;
        RelayCommand _addtoplaylistCommand;
        RelayCommand _opensonglocationCommand;
        RelayCommand _refreshViewCommand;
        RelayCommand _initCommand;
        /// <summary>
        /// Gets command for initialization. This calls the <see cref="Init(object)"/> method. <seealso cref="ICommand"/>
        /// </summary>
        public ICommand ShowPropertiesCommand
        {
            get
            { if (_showPropertiesCommand == null) { _showPropertiesCommand = new RelayCommand(param => this.ShowProperties(param)); } return _showPropertiesCommand; }
        }
        /// <summary>
        /// Gets command for initialization. This calls the <see cref="Init(object)"/> method. <seealso cref="ICommand"/>
        /// </summary>
        public ICommand InitCommand
        {
            get
            { if (_initCommand == null) { _initCommand = new RelayCommand(param => this.Init(param)); } return _initCommand; }
        }
        /// <summary>
        /// Gets command for open song location. This calls the <see cref="OpenSongLocation(object)"/> method. <seealso cref="ICommand"/>
        /// </summary>
        public ICommand OpenSongLocationCommand
        {
            get
            { if (_opensonglocationCommand == null) { _opensonglocationCommand = new RelayCommand(param => this.OpenSongLocation(param)); } return _opensonglocationCommand; }
        }
        /// <summary>
        /// Gets AddToPlaylist command. This calls the <see cref="AddToPlaylist(object)"/> method. <seealso cref="ICommand"/>
        /// </summary>
        public ICommand AddToPlaylistCommand
        {
            get
            { if (_addtoplaylistCommand == null) { _addtoplaylistCommand = new RelayCommand(param => this.AddToPlaylist(param)); } return _addtoplaylistCommand; }
        }
        /// <summary>
        /// Gets refresh command. This calls the <see cref="RefreshView(object)"/> method. <seealso cref="ICommand"/>
        /// </summary>
        public ICommand RefreshViewCommand
        {
            get
            { if (_refreshViewCommand == null) { _refreshViewCommand = new RelayCommand(param => this.RefreshView(param)); } return _refreshViewCommand; }
        }
        /// <summary>
        /// Gets Play command. This calls the <see cref="Play(object)"/> method. <seealso cref="ICommand"/>
        /// </summary>
        public ICommand PlayCommand
        {
            get
            { if (_playCommand == null) { _playCommand = new RelayCommand(param => this.Play(param)); } return _playCommand; }
        }
        /// <summary>
        /// Gets Play command. This calls the <see cref="Delete(object)"/> method. <seealso cref="ICommand"/>
        /// </summary>
        public ICommand DeleteCommand
        {
            get
            { if (_deleteCommand == null) { _deleteCommand = new RelayCommand(param => this.Delete(param)); } return _deleteCommand; }
        }
        #endregion

        #region Implementations
        async void ShowProperties(object para)
        {
            BreadPlayer.Dialogs.TagDialog tag = new BreadPlayer.Dialogs.TagDialog(para as Mediafile);
            await tag.ShowAsync();
        }

        /// <summary>
        /// Refreshes the view with new sorting order and/or filtering. <seealso cref="RefreshViewCommand"/>
        /// </summary>
        /// <param name="para"><see cref="MenuFlyoutItem"/> to get sorting/filtering base from.</param>
        void RefreshView(object para)
        {
            MenuFlyoutItem selectedItem = para as MenuFlyoutItem;
            if (selectedItem.Tag.ToString() == "genre")
            {
                Genre = selectedItem.Text;
                RefreshView(Genre, null, false);
            }
            else
            {
                RefreshView(null, selectedItem.Tag.ToString());
            }
        }
        /// <summary>
        /// Deletes a song from the FileCollection. <seealso cref="DeleteCommand"/>
        /// </summary>
        /// <param name="path"><see cref="BreadPlayer.Models.Mediafile"/> to delete.</param>
        public void Delete(object path)
        {
            if (path is ListBox)
            {
                if (FileListBox == null) FileListBox = path as ListBox;
            }
            else
            {
                var menu = path as MenuFlyoutItem;
                if (FileListBox == null) FileListBox = (menu.Tag as ListBoxItem).GetAncestorsOfType<ListBox>().ToList()[0];
            }
            var index = FileListBox.SelectedIndex;
            Mediafile mp3File = FileListBox.SelectedItem as Mediafile;
            TracksCollection.RemoveItem(mp3File);
            FileListBox.SelectedIndex = index - 1;
            FileListBox.Focus(FocusState.Programmatic);
        }
        /// <summary>
        /// Plays the selected file. <seealso cref="PlayCommand"/>
        /// </summary>
        /// <param name="path"><see cref="BreadPlayer.Models.Mediafile"/> to play.</param>
        public async void Play(object path)
        {
            Mediafile mp3File = path as Mediafile;
            if (RecentlyPlayedCollection.Elements.Any(t => t.Path == mp3File.Path))
            {
                RecentlyPlayedCollection.Elements.Remove(RecentlyPlayedCollection.Elements.First(t => t.Path == mp3File.Path));
                db.recent.Delete(t => t.Path == mp3File.Path);
            }
            RecentlyPlayedCollection.AddItem(mp3File);
            db.recent.Insert(mp3File);
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var mp3 = TracksCollection.Elements.Where(t => t.State == PlayerState.Playing);
                if (PlaylistVM.IsPageLoaded) mp3 = PlaylistVM.Songs.Where(t => t.State == PlayerState.Playing);
                if (mp3 != null) foreach(var a in mp3) a.State = PlayerState.Stopped;
                StorageFile file = await StorageFile.GetFileFromPathAsync(mp3File.path);
                ShellVM.Play(null, mp3File);
            });
            mp3File.State = PlayerState.Playing;
        }
        async void OpenSongLocation(object file)
        {
            var mp3File = file as Mediafile;
            StorageFolder folder = await (await StorageFile.GetFileFromPathAsync(mp3File.Path)).GetParentAsync();
            await Launcher.LaunchFolderAsync(folder);
        }
        void Init(object para)
        {
            NavigationService.Instance.Frame.Navigated += Frame_Navigated;
            //var childern = (para as Grid).Children;
            //var fileBox = childern.OfType<ListBox>().ToList()[0];
            //FileListBox = fileBox;
        }
        #endregion

        #endregion

        #region Methods
        /// <summary>
        /// Refresh the view, based on filters and sorting mechanisms.
        /// </summary>
        public async void RefreshView(string genre = "All genres", string propName = "Title", bool doOrderFiles = true)
        {
            IEnumerable<Mediafile> files = null;
            if (doOrderFiles)
            {
                if (propName != "Unsorted")
                {
                    files = TracksCollection.Elements.Where(t => t.Path != "").OrderBy(t => GetPropValue(t, propName)).ToList();
                    TracksCollection = null;
                    TracksCollection = new GroupedObservableCollection<string, Mediafile>(t => propName == "Title" ? (GetPropValue(t, propName) as string).Remove(1).ToUpper() : (GetPropValue(t, propName) as string), files, a => a.Title.Remove(1).ToUpper());
                }
            }
            else
            {
                //FileCollection.Clear();
                //files = genre != "All genres" ? OldItems.Where(t => t.Genre == genre) : OldItems;
                //FileCollection.AddRange(files);
            }

        }

        /// <summary>
        /// Creates genre menu.
        /// </summary>
        void CreateGenreMenu()
        {
            GenreFlyout = new MenuFlyout();
            Genre = "All genres";
            GenreFlyout.Items.Add(CreateMenuItem("All genres"));
            foreach (var genre in TracksCollection.Elements)
            {
                if (genre.Genre != null && genre.Genre != "NaN" && !GenreFlyout.Items.Any(t => (t as MenuFlyoutItem).Text == genre.Genre))
                {
                    GenreFlyout.Items.Add(CreateMenuItem(genre.Genre));
                }
            }
        }

        /// <summary>
        /// Creates menu item. <seealso cref="MenuFlyoutItem"/>
        /// </summary>
        /// <param name="genre">The text of the item.</param>
        /// <returns><see cref="MenuFlyoutItem"/></returns>
        MenuFlyoutItem CreateMenuItem(string genre)
        {
            var item = new MenuFlyoutItem() { Text = genre };
            item.Command = RefreshViewCommand;
            item.CommandParameter = item;
            item.Tag = "genre";
            return item;
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets name of a property in a class. 
        /// </summary>
        /// <param name="src">Class to search for property.</param>
        /// <param name="propName">Property to search for.</param>
        /// <returns></returns>
        public static object GetPropValue(object src, string propName)
        {
            return src.GetType().GetTypeInfo().GetDeclaredProperty(propName).GetValue(src, null);
        }

        public async Task<string> GetTextFrame(ID3v2 info, string FrameID)
        {
            // string Text = "";
            
                return await Task.Run(() =>
                {
                    if ((info.TextFrames[FrameID] as TextFrame) != null)
                        return (info.TextFrames[FrameID] as TextFrame).Text;
                    else
                        return "";
                });

        }
        /// <summary>
        /// Gets a <see cref="StorageFile"/> as a <see cref="Stream"/>.
        /// </summary>
        /// <returns><see cref="Stream"/></returns>
        public static async Task<Stream> GetFileAsStream()
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(Path);
                if (System.IO.Path.GetExtension(file.Path) == ".mp3")
                {
                    var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                    return stream.AsStream();
                }
            }
            catch { }

            return null;
        }

        #endregion

        #region Library Methods
        /// <summary>
        /// Loads library from the database file.
        /// </summary>
        async  void LoadLibrary()
        {
            if (File.Exists(ApplicationData.Current.LocalFolder.Path + @"\breadplayer.db"))
            {
                //OldItems = db.GetTracks();
                TracksCollection = new GroupedObservableCollection<string, Mediafile>(t => t.Title, await db.GetTracks(), t => t.Title);
                RecentlyPlayedCollection.AddRange(db.recent.FindAll());
                if(TracksCollection.Elements.Count > 0) MusicLibraryLoaded.Invoke(this, new RoutedEventArgs()); //no use raising an event when library isn't ready.
               ViewSource.Source = TracksCollection.Elements;
                ViewSource.IsSourceGrouped = false;
            }
        }
        private void LibraryViewModel_MusicLibraryLoaded(object sender, RoutedEventArgs e)
        {
            LoadPlaylists();
        }
        /// <summary>
        /// Asynchronously saves all the album arts in the library. 
        /// </summary>
        /// <param name="Data">ID3 tag of the song to get album art data from.</param>
        public async void SaveImages(Windows.Storage.FileProperties.StorageItemThumbnail thumb, Mediafile file)
        {
            var albumartFolder = ApplicationData.Current.LocalFolder;
            var md5Path = (file.Album + file.LeadArtist).ToLower().ToSha1();
           
            if (!File.Exists(albumartFolder.Path + @"\AlbumArts\" + md5Path + ".jpg"))
            {
                IBuffer buf;
                Windows.Storage.Streams.Buffer inputBuffer = new Windows.Storage.Streams.Buffer(1024);
                var albumart = await albumartFolder.CreateFileAsync(@"AlbumArts\" + md5Path + ".jpg", CreationCollisionOption.FailIfExists);
                using (IRandomAccessStream albumstream = await albumart.OpenAsync(FileAccessMode.ReadWrite))
                {
                    while ((buf = (await thumb.ReadAsync(inputBuffer, inputBuffer.Capacity, Windows.Storage.Streams.InputStreamOptions.None))).Length > 0)
                        await albumstream.WriteAsync(buf);

                    //try
                    //{
                    //    var data = Data.AttachedPictureFrames["APIC"] as AttachedPictureFrame;
                    //    var stream = data.Data.AsRandomAccessStream();
                    //    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                    //    var x = await decoder.GetSoftwareBitmapAsync();
                    //    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, albumstream);
                    //    encoder.SetSoftwareBitmap(x);
                    //    await encoder.FlushAsync();
                    //    stream.Dispose();
                    //}
                    //catch(Exception ex) { Debug.Write(ex.Message + "||" + file.Path); }
                }
                thumb.Dispose();
            }
        }
        #endregion

        #region Playlist Methods
        void LoadPlaylists()
        {
            Options.Add(new ContextMenuCommand(AddToPlaylistCommand, "New Playlist"));
            PlaylistCollection.AddRange(TracksCollection.Elements.SelectMany(t => t.Playlists));
            PlaylistCollection.AddRange(db.playlists.FindAll());
            foreach (var list in PlaylistCollection.DistinctBy(t => t.Name))
            {
                if (list.Songs.Count <= 0)
                    AddPlaylist(new Dictionary<Playlist, IEnumerable<Mediafile>>(), list.Name, list.Description);
                else
                {
                    var dict = new Dictionary<Playlist, IEnumerable<Mediafile>>();
                    dict.Add(list, list.Songs);
                    AddPlaylist(dict, list.Name, list.Description);
                }
            }
        }
        async void AddToPlaylist(object file)
        {
            if (file != null)
            {
                //songList is a variable to initiate both (if available) sources of songs. First is AlbumSongs and the second is the direct library songs.
                var songList = (file as MenuFlyoutItem).DataContext is Album ? ((file as MenuFlyoutItem).DataContext as Album).AlbumSongs : FileListBox.SelectedItems.AsEnumerable();
                var menu = file as MenuFlyoutItem;
                var dictPlaylist = new Dictionary<Playlist, IEnumerable<Mediafile>>();
                if (menu.Text == "New Playlist")
                {
                    dictPlaylist = await ShowAddPlaylistDialog();
                }
                foreach (Mediafile s in songList)
                {
                    var playlistName = dictPlaylist.Count > 0 ? dictPlaylist.First().Key.Name : menu.Text;
                    if (!s.Playlists.Any(t => t.Name == playlistName))
                    {
                        s.Playlists.Add(new Playlist() { Name = dictPlaylist.Count > 0 ? dictPlaylist.First().Key.Name : menu.Text, Description = dictPlaylist.Count > 0 ? dictPlaylist.First().Key.Description : "" });
                        db.Update(s);
                    }
                }
            }
            else
            {
                await ShowAddPlaylistDialog();
            }
        }

        async Task<Dictionary<Playlist, IEnumerable<Mediafile>>> ShowAddPlaylistDialog()
        {
            var dialog = new InputDialog()
            {
                Title = "Name this playlist",
            };
            var Playlists = new Dictionary<Playlist, IEnumerable<Mediafile>>();
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Text != "")
            {
                if (!PlaylistCollection.Any(t => t.Name == dialog.Text))
                {
                    AddPlaylist(Playlists, dialog.Text, dialog.Description);
                }
                return Playlists;
            }
            return Playlists;
        }
        public void AddPlaylist(Dictionary<Playlist, IEnumerable<Mediafile>> Playlists, string label, string description)
        {
            var pl = new Playlist() { Name = label, Description = description };
            Playlists.Add(pl, TracksCollection.Elements.Where(a => a.Playlists.All(t => t.Name == pl.Name) && a.Playlists.Count == 1));
            var cmd = new ContextMenuCommand(AddToPlaylistCommand, pl.Name);
            Options.Add(cmd);
            pl.Songs.AddRange(Playlists.Values.First());
            db.playlists.Insert(pl);
            ShellVM.PlaylistsItems.Add(new SplitViewMenu.SimpleNavMenuItem
            {
                Arguments = Playlists,
                Label = label,
                DestinationPage = typeof(PlaylistView),
                Symbol = Symbol.List,
                FontGlyph = "\ue823"
            });
        }
        #endregion

        double progress;
        public double Progress
        {
            get { return progress; }
            set {
                Set(ref progress, value);
            }
        }
        public event OnMusicLibraryLoaded MusicLibraryLoaded;
    }

    public delegate void OnMusicLibraryLoaded(object sender, RoutedEventArgs e);    
}
