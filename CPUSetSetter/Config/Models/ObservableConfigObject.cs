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
        private readonly HashSet<string> configExcludeProperties = []; 

        public ObservableConfigObject()
        {
            MarkConfigSaveExcludeProperties(configExcludeProperties);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName is not null && !configExcludeProperties.Contains(e.PropertyName))
                AppConfig.Instance.Save();
        }

        /// <summary>
        /// Subclasses can add property names to this collection to exclude their PropertyChanged events from trigger config saves
        /// </summary>
        protected virtual void MarkConfigSaveExcludeProperties(ICollection<string> ignoredProperties) { }

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
            if (sender is IEnumerable<ObservableConfigObject>)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                    case NotifyCollectionChangedAction.Move:
                        break; // Add and Move actions don't need any disposing
                    
                    case NotifyCollectionChangedAction.Remove:
                        if (e.OldItems![0] is ObservableConfigObject removedConfigObject)
                            removedConfigObject.Dispose();
                        break; // Dispose of 

                    case NotifyCollectionChangedAction.Replace:
                    case NotifyCollectionChangedAction.Reset:
                        throw new NotImplementedException("Collections of ObservableConfigObjects can only be added/moved/removed from");
                }
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
