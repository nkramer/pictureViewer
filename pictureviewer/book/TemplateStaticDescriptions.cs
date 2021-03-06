﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pictureviewer.Book {
    class TemplateStaticDescriptions {
        public static string data = @"
// test
//875x1125_32_1p1h0v1t:
//             m      a      g      *      m
//      m      -      -      -      -      -
//  (100)      -      -      -      -      -
//      *      -      -      -     L0      -
//  (100)      -      -      -      -      -
//      m      -      -      -      -      -

// test
//875x1125_32_1p1h0v1t:
//             *
//      *     C0

// test
//875x1125_32_1p1h0v1t:
//             m      *      m
//      m      -      -      -
//      *      -     C0      -
//      m      -      -      -
//
// test
//875x1125_32_1p1h0v1t:
//             a
//      a     L0
//
// test
//875x1125_32_1p1h0v1t:
//             a      *
//      a     L0     C1

875x1125_32_1p1h0v1t:
             m  (100)      *  (100)      m
      m      -      -      -      -      -
      a      -      -     L0      -      -
      g      -      -      -      -      -
      *      -      -     C1      -      -
      m      -      -      -      -      -

875x1125_32_2p0h2v0t_2:
             a      g      a
      a      P0      -     P1

875x1125_32_2p0h2v1t:
             a      g      a      m
      m     P0      -      -      -
      a     P0      -     C2      -
      g     P0      -      -      -
      a     P0      -     P1      -
      m     P0      -      -      -

875x1125_32_2p1h1v1t:
             m      a      g      a      m
      m      -      -      -      -      -
      a      -     L1      -     P0      -
      g      -      -      -      -      -
      *      -     C2     C2     C2      -
      m      -      -      -      -      -

875x1125_32_2p2h0v1t:
             m      *      g      a    (0)
    (0)      -      -      -      -      -
      a      -     C2      -     L0      -
      g      -     C2      -      -      -
      a      -     C2      -     L1      -
    (0)      -      -      -      -      -

875x1125_32_3p0h3v0t:
             a      g      a      g      a      
      a      P0      -     P1      -     P2      

875x1125_32_3p0h3v1t:
             m      a      g      a      g      a      m
      m      -      -      -      -      -      -      -
      a      -     P0      -     P1      -     P2      -
      g      -      -      -      -      -      -      -
      *      -     C3     C3     C3     C3     C3      -
      m      -      -      -      -      -      -      -

875x1125_32_3p0h3v1t_hack:
             m      a      g      a      g      a      m
   (55)      -      -      -      -      -      -      -
      a      -     P0      -     P1      -     P2      -
      g      -      -      -      -      -      -      -
      *      -     C3     C3     C3     C3     C3      -
      m      -      -      -      -      -      -      -

875x1125_32_3p0h3v1t_hack2:
             m      a      g      a      g      a      g      a      m
      m      -      -      -      -      -      -      -      -      -
      a      -     P0      -     P1     P1     P1      -     P2      -
      g      -      -      -      -      -      -      -      -      -
      *      -     C3     C3     C3      -     C4     C4     C4      -
      m      -      -      -      -      -      -      -      -      -
      and col3=col5

875x1125_32_3p2h1v0t:
             a      g      a
      a     P0      -     L1
      g     P0      -      -
      a     P0      -     L2

875x1125_32_3p2h1v0t_2:
             a      g      a      
      a      P0      -     L1      
      g      P0      -      -      
      a      P0      -     L2      

875x1125_32_3p3h0v1t:
           m    a      g      a      m
      m    -    -      -      -      -
      a    -    C3     -     L0      -
      g    -    -      -      -      -
      a    -    L2     -     L1      -
      m    -    -      -      -      -

875x1125_32_3p3h0v1t_2:
             m      *     g      a      m
      a      -     C3     -     L0      -
      g      -     C3     -      -      -
      a      -     C3     -     L1      -
      g      -     C3     -      -      -
      a      -     C3     -     L2      -

875x1125_32_4p2h2v0t:
                   a      g      a      g      a      
      a           L0     L0     L0      -     P1     
      g            -      -      -      -      -      
      a           P3      -     L2     L2     L2      

// misnamed
875x1125_32_4p2h2v0t_2:
             a      g      a      g      a      
      *      P0      -      -      -      -      
      a      P0      -     L1     L1     L1      
      g      P0      -      -      -      -      
      a      P0      -     P3      -     P2      
      *      P0      -      -      -      -      
    and row0=row4

875x1125_32_4p2h2v0t2:
             m      a      g      a      g      a      *
      a      -     L0     L0     L0      -     P1     C4
      g      -      -      -      -      -      -     C4
      a      -     P3      -     L2     L2     L2     C4

875x1125_32_4p3h1v0t:
             a      g      a      g      a
      a     P0      -     L1     L1     L1
      g      -      -      -      -      -
      a     L3     L3     L3      -     P2

875x1125_32_6p0h6v0t:
              a      g      a      g      a      
      a      P0      -     P1      -     P2      
      g       -      -      -      -      -      
      a      P3      -     P4      -     P5      

875x1125_32_6p0h6v1t:
             a      g      a      g      a      g      *  
      a     P0      -     P1      -     P2      -     C6      
      g      -      -      -      -      -      -     C6      
      a     P3      -     P4      -     P5      -     C6      

875x1125_32_6p6h0v0t:
                   a      g      a      g      a      
      a           L0      -     L1      -     L2      
      g            -      -      -      -      -      
      a           L3      -     L4      -     L5      

875x1125_32_6p6h0v1t:
                   a      g      a      g      a
      a           L0      -     L1      -     L2      
      g            -      -      -      -      -      
      a           L3      -     L4      -     L5      
      g            -      -      -      -      -      
      a           C6     C6     C6     C6     C6      

875x1125_32_6p6h0v1t_2:
                    a      g      a      g      a
       a           L0      -     L1      -     C6      
       g            -      -      -      -     C6      
       a           L2      -     L3      -     C6      
       g            -      -      -      -     C6      
       a           L4      -     L5      -     C6      

875x1125_32_9p9h0v0t:
                    a     g      a      g      a      
      a           L0      -     L1      -     L2      
      g            -      -      -      -      -      
      a           L3      -     L4      -     L5      
      g            -      -      -      -      -      
      a           L6      -     L7      -     L8      
";
    }
}
