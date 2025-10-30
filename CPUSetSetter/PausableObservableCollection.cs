using System.Collections.ObjectModel;
using System.Collections.Specialized;


namespace CPUSetSetter
{
    public class PausableObservableCollection<T> : ObservableCollection<T>
    {
        private bool suppressNotifications = false;

        public void SuppressNotifications(bool suppress)
        {
            suppressNotifications = suppress;
            if (!suppress)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Reset));
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!suppressNotifications)
            {
                base.OnCollectionChanged(e);
            }
        }
    }
}
