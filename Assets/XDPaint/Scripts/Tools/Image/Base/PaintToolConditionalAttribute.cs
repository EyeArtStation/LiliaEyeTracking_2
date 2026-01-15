using System;

namespace XDPaint.Core.PaintObject.Base
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class PaintToolConditionalAttribute : Attribute
    {
        public string Condition;
        public bool Inverse;
        
        public PaintToolConditionalAttribute(string condition)
        {
            Condition = condition;
        }
        
        public PaintToolConditionalAttribute(string condition, bool inverse)
        {
            Condition = condition;
            Inverse = inverse;
        }
    }
}
