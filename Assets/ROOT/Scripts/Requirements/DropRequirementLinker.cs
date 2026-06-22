using UnityEngine;

namespace ROOT.Scripts
{
    /// <summary>
    /// Fulfilled khi DropZone nhận đúng item.
    /// Gắn vào cùng GameObject với DropZone.
    /// Hoặc kéo DropZone vào field _dropZone.
    /// </summary>
    public class DropRequirementLinker : RequirementLinker
    {
        [SerializeField] private DropZone _dropZone;
        [SerializeField] private string _requiredItemID; // rỗng = chấp nhận bất kỳ item nào vào zone này

        protected override void RegisterListeners()
        {
            if (_dropZone == null)
                _dropZone = GetComponent<DropZone>();

            if (_dropZone != null)
                _dropZone.OnItemAccepted.AddListener(OnItemDropped);
        }

        private void OnItemDropped(Draggable draggable)
        {
            if (string.IsNullOrEmpty(_requiredItemID) || draggable.ItemID == _requiredItemID)
                Fulfill();
        }

        protected override void OnReset()
        {
            _dropZone?.Clear();
        }
    }
}
