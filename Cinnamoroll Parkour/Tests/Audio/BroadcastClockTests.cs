using System;
class Tests {
 static void Main() {
  foreach(double fps in new double[]{15,30,60,144}) {
   var clock=new BroadcastAudioClock(32);double frame=1/fps,t=0,old=0,newMax=0,oldMax=0;int next=1;double deadline=.5;
   while(t<30) {
    t+=frame;double elapsed=clock.Observe(Math.Floor(t*1000)/1000);
    if(elapsed>=next*.5){newMax=Math.Max(newMax,elapsed-next*.5);next++;}
    if(t>=deadline){oldMax=Math.Max(oldMax,t-(old+1)*.5);old++;deadline=t+.5;}
   }
   if(newMax>frame+.001)throw new Exception("drift");
   Console.WriteLine($"{fps}fps: legacy max drift {oldMax*1000:F1}ms, fixed max {newMax*1000:F1}ms");
  }
  foreach(double fps in new double[]{30,60}) {
   var stressed=new BroadcastAudioClock(32);double t=0,frame=1/fps,lateMax=0,postStallMax=0;int next=1;bool injected=false;
   while(t<15) {
    double dt=frame;if(!injected && t>=3.4){dt=.25;injected=true;}t+=dt;
    double elapsed=stressed.Observe(Math.Floor(t*1000)/1000);
    if(elapsed>=next*.5){double late=elapsed-next*.5;lateMax=Math.Max(lateMax,late);if(t>4)postStallMax=Math.Max(postStallMax,late);next++;}
   }
   if(postStallMax>frame+.001)throw new Exception("stall recovery drift");
   Console.WriteLine($"{fps}fps + 250ms stall: max lateness {lateMax*1000:F1}ms; after recovery {postStallMax*1000:F1}ms");
  }
  var loop=new BroadcastAudioClock(32);loop.Observe(31.9);if(Math.Abs(loop.Observe(.1)-32.1)>.001)throw new Exception("loop");
  if(loop.Observe(.09)<32.1)throw new Exception("backwards jitter");
  Console.WriteLine("loop and backward-query jitter PASS");
 }
}