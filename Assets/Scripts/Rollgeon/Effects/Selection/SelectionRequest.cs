using System;
using System.Collections.Generic;

namespace Rollgeon.Effects.Selection
{
    public class SelectionRequest
    {
        public SelectionSettings Settings;
        public List<TargetRef> ValidTargets;
        public Guid OwnerGuid;
        public string HighlightStyle;
    }
}
