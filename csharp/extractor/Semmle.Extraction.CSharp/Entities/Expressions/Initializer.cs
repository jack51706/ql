using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Semmle.Extraction.CSharp.Populators;
using Semmle.Extraction.Entities;
using Semmle.Extraction.Kinds;

namespace Semmle.Extraction.CSharp.Entities.Expressions
{
    abstract class Initializer : Expression<InitializerExpressionSyntax>
    {
        protected Initializer(ExpressionNodeInfo info) : base(info) { }
    }

    class ArrayInitializer : Expression<InitializerExpressionSyntax>
    {
        ArrayInitializer(ExpressionNodeInfo info) : base(info.SetType(Type.Create(info.Context, null)).SetKind(ExprKind.ARRAY_INIT)) { }

        public static Expression Create(ExpressionNodeInfo info) => new ArrayInitializer(info).TryPopulate();

        protected override void Populate()
        {
            var child = 0;
            foreach (var e in Syntax.Expressions)
            {
                if (e.Kind() == SyntaxKind.ArrayInitializerExpression)
                {
                    // Recursively create another array initializer
                    Create(new ExpressionNodeInfo(cx, (InitializerExpressionSyntax)e, this, child++));
                }
                else
                {
                    // Create the expression normally.
                    Create(cx, e, this, child++);
                }
            }
        }
    }

    // Array initializer { ..., ... }.
    class ImplicitArrayInitializer : Initializer
    {
        ImplicitArrayInitializer(ExpressionNodeInfo info) : base(info.SetKind(ExprKind.ARRAY_CREATION)) { }

        public static Expression Create(ExpressionNodeInfo info) => new ImplicitArrayInitializer(info).TryPopulate();

        protected override void Populate()
        {
            ArrayInitializer.Create(new ExpressionNodeInfo(cx, Syntax, this, -1));
            cx.Emit(Tuples.implicitly_typed_array_creation(this));
        }
    }

    class ObjectInitializer : Initializer
    {
        ObjectInitializer(ExpressionNodeInfo info)
            : base(info.SetKind(ExprKind.OBJECT_INIT)) { }

        public static Expression Create(ExpressionNodeInfo info) => new ObjectInitializer(info).TryPopulate();

        protected override void Populate()
        {
            var child = 0;

            foreach (var init in Syntax.Expressions)
            {
                var assignment = init as AssignmentExpressionSyntax;

                if (assignment != null)
                {
                    var assignmentEntity = new Expression(new ExpressionNodeInfo(cx, init, this, child++).SetKind(ExprKind.SIMPLE_ASSIGN));

                    CreateFromNode(new ExpressionNodeInfo(cx, assignment.Right, assignmentEntity, 0));

                    var target = cx.GetSymbolInfo(assignment.Left);
                    if (target.Symbol == null)
                    {
                        cx.ModelError(assignment, "Unknown object initializer");
                        new Unknown(new ExpressionNodeInfo(cx, assignment.Left, assignmentEntity, 1));
                    }
                    else
                    {
                        Access.Create(new ExpressionNodeInfo(cx, assignment.Left, assignmentEntity, 1), target.Symbol, false, cx.CreateEntity(target.Symbol));
                    }
                }
                else
                {
                    cx.ModelError(init, "Unexpected object initialization");
                    Create(cx, init, this, child++);
                }
            }
        }
    }

    class CollectionInitializer : Initializer
    {
        CollectionInitializer(ExpressionNodeInfo info) : base(info.SetKind(ExprKind.COLLECTION_INIT)) { }

        public static Expression Create(ExpressionNodeInfo info) => new CollectionInitializer(info).TryPopulate();

        protected override void Populate()
        {
            var child = 0;
            foreach (var i in Syntax.Expressions)
            {
                var collectionInfo = cx.Model(Syntax).GetCollectionInitializerSymbolInfo(i);
                var addMethod = Method.Create(cx, collectionInfo.Symbol as IMethodSymbol);
                var voidType = Type.Create(cx, cx.Compilation.GetSpecialType(SpecialType.System_Void));

                var invocation = new Expression(new ExpressionInfo(cx, voidType, cx.Create(i.GetLocation()), ExprKind.METHOD_INVOCATION, this, child++, false, null));

                if (addMethod != null)
                    cx.Emit(Tuples.expr_call(invocation, addMethod));
                else
                    cx.ModelError(Syntax, "Unable to find an Add() method for collection initializer");

                if (i.Kind() == SyntaxKind.ComplexElementInitializerExpression)
                {
                    // Arrays of the form new Foo { { 1,2 }, { 3, 4 } }
                    // where the arguments { 1, 2 } are passed to the Add() method.

                    var init = (InitializerExpressionSyntax)i;

                    int addChild = 0;
                    foreach (var arg in init.Expressions)
                    {
                        Create(cx, arg, invocation, addChild++);
                    }
                }
                else
                {
                    Create(cx, i, invocation, 0);
                }
            }
        }
    }
}
