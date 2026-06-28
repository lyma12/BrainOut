using System.Collections.Generic;
using UnityEngine;

namespace ROOT.Scripts
{
    /// <summary>
    /// Fulfilled khi DropZone nhận đúng item.
    /// _acceptedIDs và _lockOnAccept được editor bake vào từ RequirementData.
    /// </summary>
    public class DropRequirementLinker : RequirementLinker
    {
        [SerializeField] private DropZone _dropZone;
        [SerializeField] private List<string> _acceptedIDs = new List<string>();

        /// <summary>Khi true, item sẽ bị lock (IsInteractable = false) sau khi drop đúng.</summary>
        [SerializeField] private bool _lockOnAccept = false;

        protected override void RegisterListeners()
        {
            if (_dropZone == null)
                _dropZone = GetComponent<DropZone>();

            if (_dropZone != null)
                _dropZone.OnItemAccepted.AddListener(OnItemDropped);
        }

        private void OnItemDropped(Snappable snappable)
        {
            if (_acceptedIDs != null && _acceptedIDs.Count > 0)
            {
                var targetID = snappable.GetComponent<ActionTargetID>();
                if (targetID == null || !_acceptedIDs.Contains(targetID.ID)) return;
            }

            if (_lockOnAccept)
                snappable.IsInteractable = false;

            Fulfill();
        }

        protected override void OnReset()
        {
            _dropZone?.Clear();
        }
    }
}
