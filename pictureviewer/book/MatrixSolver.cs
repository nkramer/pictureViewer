using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Pictureviewer.Book {
    class MatrixSolver {
        private static readonly double EPSILON = 1e-10;

        // Gaussian elimination with partial pivoting -- Ax = b, where x is return value
        // based on http://introcs.cs.princeton.edu/java/95linear/GaussianElimination.java.html
        // extended to non-square matrices
        // A[row][col]
        // null if not solvable
        public static double[] SolveLinearEquations(double[][] A, double[] b) {
            //Debug.WriteLine("new matrix");
            //DebugPrintMatrix(A, b);
            double[][] originalA = A.Select(row => (double[])row.Clone()).ToArray();
            double[] originalB = (double[]) b.Clone();

            int nrow = A.Length; // m in mxn
            int ncol = A[0].Length; // n in mxn
            Debug.Assert(nrow == b.Length);

            if (nrow < ncol) {
                // DebugPrintMatrix(A, b);
                return null; // more variables than equations
            }

            GaussianElimination(A, b);

            //DebugPrintMatrix(A, b);
            double[] x = BackSubstitution(A, b);
            if (x == null) { // not solvable
                // DebugPrintMatrix(A, b);
                return null;
            }

            //DebugPrintMatrix(A, b);
            VerifySolution(originalA, originalB, x);
            return x;
        }

        private static void GaussianElimination(double[][] A, double[] b) {
            int nrow = A.Length; // m in mxn
            int ncol = A[0].Length; // n in mxn
            for (int pivotRow = 0; pivotRow < nrow; pivotRow++) {
                Debug.Assert(A[pivotRow].Length == ncol);
                //DebugPrintMatrix(A, b);
                // find pivot row for row p and swap
                int maxrow = pivotRow;
                int pivotCol;
                bool nonzeroDataFound = false;
                for (pivotCol = pivotRow; pivotCol < ncol; pivotCol++) {
                    maxrow = FindMaxRow(A, pivotRow, pivotCol);
                    if (!CloseEnough(A[maxrow][pivotCol], 0)) {
                        nonzeroDataFound = true;
                        break;
                    }
                }

                if (nonzeroDataFound) {
                    Swap(ref A[pivotRow], ref A[maxrow]);
                    Swap(ref b[pivotRow], ref b[maxrow]);

                    // pivot within A and b -- Do for all rows below pivot
                    // in theory, any rows in excess of # cols is redundant and can be ignored
                    for (int i = pivotRow + 1; i < nrow; i++) {
                        //Debug.WriteLine("");
                        //DebugPrintMatrix(A, b);

                        double alpha = A[i][pivotCol] / A[pivotRow][pivotCol];
                        b[i] -= alpha * b[pivotCol];
                        for (int j = pivotCol; j < ncol; j++) {
                            A[i][j] -= alpha * A[pivotRow][j];
                        }
                        Debug.Assert(CloseEnough(A[i][pivotCol], 0));
                        A[i][pivotCol] = 0; // not necessary but makes the code more obvious

                        if (double.IsNaN(b[i]))
                            Debug.Fail("debug me!");
                    }
                    //Debug.Assert(CloseEnough(A[pivotRow][pivotCol], 1.0)); // fails -- confirm assert is correct
                }
            }
        }

        private static double[] BackSubstitution(double[][] A, double[] b) {
            int nrow = A.Length; // m in mxn
            int ncol = A[0].Length; // n in mxn

            double[] x = new double[ncol];
            for (int i = 0; i < ncol; i++) {
                x[i] = double.NaN;
            }

            for (int i = nrow - 1; i >= 0; i--) {
                if (i >= ncol) {
                    if (A[i].Any(num => !CloseEnough(num, 0)) || !CloseEnough(b[i], 0)) {
                        // rows > # cols should be 0 if the system is solvable
                        return null;
                        //Debug.Fail("no solution");
                    }
                } else {
                    // find 1st non-zero col in row
                    int col = i;
                    bool foundNonzeroColumn = false;
                    for (col = i; col < ncol; col++) {
                        if (!CloseEnough(A[i][col], 0)) {
                            foundNonzeroColumn = true;
                            break;
                        }
                    }

                    if (foundNonzeroColumn) {
                        double sum = 0.0;
                        for (int j = col + 1; j < ncol; j++) {
                            Debug.Assert(!double.IsNaN(A[i][j]));
                            if (double.IsNaN(x[j])) {
                                // matrix is unsolvable, we're trying to compute the col'th variable & the col+1'th is still unknown
                                return null;
                            }
                            sum += A[i][j] * x[j];
                            A[i][j] = 0; // not strictly necessary but makes it more obvious
                            Debug.Assert(!double.IsNaN(sum));
                        }
                        if (A[i][col] == 0) {
                            Debug.Fail("debug me!");
                        }
                        x[i] = (b[i] - sum) / A[i][col];

                        // not strictly necessary but makes it more obvious
                        b[i] = x[i];
                        if (double.IsNaN(b[i]))
                            Debug.Fail("debug me!");
                        A[i][col] = 1.0;
                    }
                }
            }
            return x;
        }

        private static void Swap<T>(ref T left, ref T right) {
            T temp;
            temp = left;
            left = right;
            right = temp;
        }
        
        private static void VerifySolution(double[][] A, double[] b, double[] solution) {
            for (int i = 0; i < A.Length; i++) {
                double sum = 0;
                for (int j = 0; j < A[i].Length; j++) {
                    sum += A[i][j] * solution[j];
                }
                if (!CloseEnough(b[i], sum))
                    Debug.Fail("");
            }
        }

        public static bool CloseEnough(double a, double b) {
            return Math.Abs(a - b) < EPSILON;
        }

        // maxrow = row w/ largest leading #
        private static int FindMaxRow(double[][] A, int pivotRow, int pivotCol) {
            int nrow = A.Length; // m in mxn
            int maxrow = pivotRow;
            for (int i = pivotRow + 1; i < nrow; i++) {
                if (Math.Abs(A[i][pivotCol]) > Math.Abs(A[maxrow][pivotCol])) {
                    maxrow = i;
                }
            }

            return maxrow;
        }

        private void Test() {
            double[][] A = new double[][] { 
                         new double[] { 0, 1,  1 },
                         new double[] { 2, 4, -2 },
                         new double[] { 0, 3, 15 },
                         new double[] { 1, 2, 3 },
                       };
            double[] b = { 4, 2, 36, 1 * -1 + 2 * 2 + 3 * 2 };
            //double[] x = lsolveOrig(A, b);
            double[] x = SolveLinearEquations(A, b);

            // expected answer:
            //*  -1.0
            //*  2.0
            //*  2.0
        }

        public static void DebugPrintMatrix(double[][] A, double[] b) {
            Debug.WriteLine("");
            for (int i = 0; i < A.Length; i++) {
                string Aoutput = string.Join(" ", A[i].Select(y => {
                    if (CloseEnough(y, 0)) {
                        return "     0    ";
                    } else if (CloseEnough(y, 1)) {
                        Debug.Assert(y > 0);
                        return "     1    ";
                    } else {
                        return string.Format("{0,10:f5}", y);
                    }
            }
                )); // 10 chars wide per #
                Debug.WriteLine(Aoutput + " " + b[i]);
            }
            Debug.WriteLine("");
        }
    }
}
