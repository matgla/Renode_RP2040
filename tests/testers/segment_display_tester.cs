/**
 * segment_display_tester.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;

using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Config;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Migrant.Hooks;
using Antmicro.Migrant;


namespace Antmicro.Renode.Testing
{
   public static class SegmentDisplayTesterExtension
   {
      public static void CreateSegmentDisplayTester(this Emulation emulation, string name, ISegmentDisplay display, float timeoutInSeconds)
      {
         var tester = new SegmentDisplayTester(TimeSpan.FromSeconds(timeoutInSeconds), display);
         emulation.ExternalsManager.AddExternal(tester, name);
      }
   }


   public class SegmentDisplayTester : IExternal
   {
      public SegmentDisplayTester(TimeSpan timeout, ISegmentDisplay display)
      {
         this.display = display;
      }

      public SegmentDisplayTester WaitForSequence(string filepath, float? timeout=null)
      {
         string text = File.ReadAllText(filepath);
         return this;

         // var timeoutForSequence = TimeSpan.FromSeconds(timeout.Value) ?? defaultTimeout;
         // var timeoutEvent = machine.LocalTimeSource.EnqueueTimeoutEvent((ulong)timeoutForSequence.TotalMilliseconds);

         // do 
         // {

         // } while(!timeoutEvent.IsTriggered);
      }

      private ISegmentDisplay display;

   }



}
