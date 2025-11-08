using System.Collections.ObjectModel;
using System.Collections.Specialized;


namespace CPUSetSetter.UI
{
    public class PausableObservableCollection<T> : ObservableCollection<T>
    {
        private bool suppressNotifications = false;

        public void SuppressNotifications(bool suppress)
        {
            suppressNotifications = suppress;
            if (!suppress)
            {
                // Signal that the collection has to be re-read, as processes could have been created/removed during the pause
                OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            // Don't announce that the collection has changed when it is paused
            if (!suppressNotifications)
            {
                base.OnCollectionChanged(e);
            }
        }
    }
}
