// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace Groovy_GH
{
    public class Groovy_GHInfo : GH_AssemblyInfo
    {
        public override string Name => "Groovy_GH";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "Audio-driven kinetic panel motion system";

        public override Guid Id => new("55575999-5770-4204-b960-69b820cfe712");

        public override string AuthorName => "Groovy_GH";

        public override string AuthorContact => "";

        public override string AssemblyVersion =>
            GetType().Assembly.GetName().Version?.ToString() ?? "1.0";
    }
}