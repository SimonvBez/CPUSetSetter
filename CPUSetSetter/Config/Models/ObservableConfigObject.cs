using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;


namespace CPUSetSetter.Config.Models
{
    /// <summary>
    /// Helper class to make it easier to automatically save on any changes
    /// </summary>
    public class ObservableConfigObject : ObservableObject, IDisposable
    {
        private readonly List<Action> collectionUnsubscribeActions = [];

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            AppConfig.Instance.Save();
        }

        /// <summary>
        /// Automatically save the config file when something in the collection changes
        /// </summary>
        protected void SaveOnCollectionChanged<T>(ObservableCollection<T> observableCollection)
        {
            observableCollection.CollectionChanged += SaveConfig;
            collectionUnsubscribeActions.Add(() => observableCollection.CollectionChanged -= SaveConfig);
        }

        private static void SaveConfig(object? sender, NotifyCollectionChangedEventArgs e)
        {
            AppConfig.Instance.Save();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Unsubscribe from the CollectionChanged event to not hold back the GC
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            foreach (Action unsubAction in collectionUnsubscribeActions)
            {
                unsubAction();
            }
            collectionUnsubscribeActions.Clear();
        }
    }
}
