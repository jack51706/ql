private import cpp
private import DataFlowPrivate

Function viableImpl(MethodAccess ma) {
  result = viableCallable(ma)
}

/**
 * Gets a function that might be called by `call`.
 */
Function viableCallable(Call call) {
  result = call.getTarget()
  or
  // If the target of the call does not have a body in the snapshot, it might
  // be because the target is just a header declaration, and the real target
  // will be determined at run time when the caller and callee are linked
  // together by the operating system's dynamic linker. In case a _unique_
  // function with the right signature is present in the database, we return
  // that as a potential callee.
  exists(string qualifiedName, int nparams |
    callSignatureWithoutBody(qualifiedName, nparams, call) and
    functionSignatureWithBody(qualifiedName, nparams, result) and
    strictcount(Function other | functionSignatureWithBody(qualifiedName, nparams, other)) = 1
  )
}

/**
 * Holds if `f` is a function with a body that has name `qualifiedName` and
 * `nparams` parameter count. See `functionSignature`.
 */
private predicate functionSignatureWithBody(string qualifiedName, int nparams, Function f) {
  functionSignature(f, qualifiedName, nparams) and
  exists(f.getBlock())
}

/**
 * Holds if the target of `call` is a function _with no definition_ that has
 * name `qualifiedName` and `nparams` parameter count. See `functionSignature`.
 */
pragma[noinline]
private predicate callSignatureWithoutBody(string qualifiedName, int nparams, Call call) {
  exists(Function target |
    target = call.getTarget() and
    not exists(target.getBlock()) and
    functionSignature(target, qualifiedName, nparams)
  )
}

/**
 * Holds if `f` has name `qualifiedName` and `nparams` parameter count. This is
 * an approximation of its signature for the purpose of matching functions that
 * might be the same across link targets.
 */
private predicate functionSignature(Function f, string qualifiedName, int nparams) {
  qualifiedName = f.getQualifiedName() and
  nparams = f.getNumberOfParameters() and
  not f.isStatic()
}

/**
 * Holds if the call context `ctx` reduces the set of viable dispatch
 * targets of `ma` in `c`.
 */
predicate reducedViableImplInCallContext(MethodAccess ma, Callable c, Call ctx) {
  none()
}

/**
 * Gets a viable dispatch target of `ma` in the context `ctx`. This is
 * restricted to those `ma`s for which a context might make a difference.
 */
private Method viableImplInCallContext(MethodAccess ma, Call ctx) {
  // stub implementation
  result = viableImpl(ma) and
  viableCallable(ctx) = ma.getEnclosingFunction()
}

/**
 * Gets a viable dispatch target of `ma` in the context `ctx`. This is
 * restricted to those `ma`s for which the context makes a difference.
 */
Method prunedViableImplInCallContext(MethodAccess ma, Call ctx) {
  result = viableImplInCallContext(ma, ctx) and
  reducedViableImplInCallContext(ma, _, ctx)
}

/**
 * Holds if flow returning from `m` to `ma` might return further and if
 * this path restricts the set of call sites that can be returned to.
 */
predicate reducedViableImplInReturn(Method m, MethodAccess ma) {
  exists(int tgts, int ctxtgts |
    m = viableImpl(ma) and
    ctxtgts = count(Call ctx | m = viableImplInCallContext(ma, ctx)) and
    tgts = strictcount(Call ctx | viableCallable(ctx) = ma.getEnclosingFunction()) and
    ctxtgts < tgts
  )
}

/**
 * Gets a viable dispatch target of `ma` in the context `ctx`. This is
 * restricted to those `ma`s and results for which the return flow from the
 * result to `ma` restricts the possible context `ctx`.
 */
Method prunedViableImplInCallContextReverse(MethodAccess ma, Call ctx) {
  result = viableImplInCallContext(ma, ctx) and
  reducedViableImplInReturn(result, ma)
}
