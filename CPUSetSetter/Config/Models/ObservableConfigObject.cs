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
            observableCollection.CollectionChanged += OnObservableConfigCollectionChanged;
            collectionUnsubscribeActions.Add(() => observableCollection.CollectionChanged -= OnObservableConfigCollectionChanged);
        }

        private static void OnObservableConfigCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Save the config with the made changes
            AppConfig.Instance.Save();

            // Dispose of any ObservableConfigObject that may have been removed
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                if (e.OldItems![0] is ObservableConfigObject removedConfigObject)
                    removedConfigObject.Dispose();
            }
            else if (e.Action != NotifyCollectionChangedAction.Add)
            {
                throw new NotImplementedException("Collections of ObservableConfigObjects should only be added/removed from");
            }
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
            if (disposing)
            {
                foreach (Action unsubAction in collectionUnsubscribeActions)
                {
                    unsubAction();
                }
                collectionUnsubscribeActions.Clear();
            }
        }
    }
}
