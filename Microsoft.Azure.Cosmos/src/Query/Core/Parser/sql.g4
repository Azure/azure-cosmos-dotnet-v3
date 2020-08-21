grammar sql;

program
	: sql_query EOF
	;

sql_query : select_clause from_clause? where_clause? group_by_clause? order_by_clause? offset_limit_clause? ;

/*--------------------------------------------------------------------------------*/
/* SELECT */
/*--------------------------------------------------------------------------------*/
select_clause : K_SELECT K_DISTINCT? top_spec? selection ;
top_spec : K_TOP NUMERIC_LITERAL;
selection
	: select_star_spec
	| select_value_spec 
	| select_list_spec
	;
select_star_spec : '*' ;
select_value_spec : K_VALUE scalar_expression ;
select_list_spec : select_item ( ',' select_item )* ;
select_item : scalar_expression (K_AS IDENTIFIER)? ;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* FROM */
/*--------------------------------------------------------------------------------*/
from_clause : K_FROM collection_expression ;
collection_expression
	: collection (K_AS IDENTIFIER)? #AliasedCollectionExpression
	| IDENTIFIER K_IN collection #ArrayIteratorCollectionExpression 
	| collection_expression K_JOIN collection_expression #JoinCollectionExpression
	;
collection
	: IDENTIFIER path_expression?  #InputPathCollection
	| '(' sql_query ')' #SubqueryCollection
	;
path_expression
	: path_expression'.'IDENTIFIER #IdentifierPathExpression
	| path_expression'[' NUMERIC_LITERAL ']' #NumberPathExpression
	| path_expression'[' STRING_LITERAL ']' #StringPathExpression
	| /*epsilon*/ #EpsilonPathExpression
	;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* WHERE */
/*--------------------------------------------------------------------------------*/
where_clause : K_WHERE scalar_expression ;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* GROUP BY */
/*--------------------------------------------------------------------------------*/
group_by_clause : K_GROUP K_BY scalar_expression_list ;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* ORDER BY */
/*--------------------------------------------------------------------------------*/
order_by_clause : K_ORDER K_BY order_by_items ;
order_by_items : order_by_item (',' order_by_item)* ;
order_by_item : scalar_expression sort_order? ;
sort_order
	: K_ASC
	| K_DESC
	;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* OFFSET LIMIT */
/*--------------------------------------------------------------------------------*/
offset_limit_clause : K_OFFSET offset_count K_LIMIT limit_count;
offset_count : NUMERIC_LITERAL;
limit_count : NUMERIC_LITERAL;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* SCALAR EXPRESSIONs */
/*--------------------------------------------------------------------------------*/
scalar_expression
	: '[' scalar_expression_list? ']' #ArrayCreateScalarExpression
	| K_ARRAY '(' sql_query ')' #ArrayScalarExpression
	| scalar_expression K_NOT? K_BETWEEN scalar_expression K_AND scalar_expression #BetweenScalarExpression
	| scalar_expression binary_operator scalar_expression #BinaryScalarExpression
	| scalar_expression '??' scalar_expression #CoalesceScalarExpression
	| scalar_expression '?' scalar_expression ':' scalar_expression #ConditionalScalarExpression
	| K_EXISTS '(' sql_query ')' #ExistsScalarExpression
	| (K_UDF '.')? IDENTIFIER '(' scalar_expression_list? ')' #FunctionCallScalarExpression
	| scalar_expression K_NOT? K_IN '(' scalar_expression_list ')' #InScalarExpression
	| literal #LiteralScalarExpression
	| scalar_expression '[' scalar_expression ']' #MemberIndexerScalarExpression
	| '{' object_propertty_list? '}' #ObjectCreateScalarExpression
	| IDENTIFIER #PropertyRefScalarExpressionBase
	| scalar_expression '.' IDENTIFIER #PropertyRefScalarExpressionRecursive
	| '(' sql_query ')' #SubqueryScalarExpression
	| unary_operator scalar_expression #UnaryScalarExpression
	;
scalar_expression_list : scalar_expression ( ',' scalar_expression )* ;
binary_operator
	: '+' 
	| K_AND 
	| '&' 
	| '|' 
	| '^' 
	| '/' 
	| '=' 
	| '>' 
	| '>=' 
	| '<' 
	| '<=' 
	| '%' 
	| '*' 
	| '!=' 
	| K_OR 
	| '||' 
	| '-'
	;
unary_operator
	: '-' 
	| '+' 
	| '~' 
	| K_NOT
	;
object_propertty_list : object_property (',' object_property)* ;

object_property : STRING_LITERAL ':' scalar_expression ;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* KEYWORDS */
/*--------------------------------------------------------------------------------*/
K_AND : A N D;
K_ARRAY : A R R A Y;
K_AS : A S;
K_ASC : A S C;
K_BETWEEN : B E T W E E N;
K_BY : B Y;
K_DESC : D E S C;
K_DISTINCT : D I S T I N C T;
K_EXISTS : E X I S T S;
K_FALSE : 'false';
K_FROM : F R O M;
K_GROUP : G R O U P;
K_IN : I N ;
K_JOIN : J O I N;
K_LIMIT : L I M I T;
K_NOT : N O T;
K_NULL : 'null';
K_OFFSET : O F F S E T;
K_OR : O R;
K_ORDER : O R D E R;
K_SELECT : S E L E C T;
K_TOP : T O P;
K_TRUE : 'true';
K_UDF : U D F;
K_UNDEFINED : 'undefined';
K_VALUE : V A L U E;
K_WHERE : W H E R E;
/*--------------------------------------------------------------------------------*/

WS
   : [ \r\n\t] + -> skip
   ;

/*--------------------------------------------------------------------------------*/
/* LITERALS */
/*--------------------------------------------------------------------------------*/
literal
	: STRING_LITERAL
	| NUMERIC_LITERAL
	| K_TRUE
	| K_FALSE
	| K_NULL
	| K_UNDEFINED
	;

NUMERIC_LITERAL
	: ( '+' | '-' )? DIGIT+ ( '.' DIGIT* )? ( E [-+]? DIGIT+ )?
	| ( '+' | '-' )? '.' DIGIT+ ( E [-+]? DIGIT+ )?
	;

STRING_LITERAL
	: ('\'' | '"') ( ~'\'' | '\'\'' )* ('\'' | '"')
	;

IDENTIFIER
	:
	| [a-zA-Z_][a-zA-Z_]*DIGIT*
	;
/*--------------------------------------------------------------------------------*/

/*--------------------------------------------------------------------------------*/
/* FRAGMENTS */
/*--------------------------------------------------------------------------------*/
fragment DIGIT : [0-9];

fragment A : [aA];
fragment B : [bB];
fragment C : [cC];
fragment D : [dD];
fragment E : [eE];
fragment F : [fF];
fragment G : [gG];
fragment H : [hH];
fragment I : [iI];
fragment J : [jJ];
fragment K : [kK];
fragment L : [lL];
fragment M : [mM];
fragment N : [nN];
fragment O : [oO];
fragment P : [pP];
fragment Q : [qQ];
fragment R : [rR];
fragment S : [sS];
fragment T : [tT];
fragment U : [uU];
fragment V : [vV];
fragment W : [wW];
fragment X : [xX];
fragment Y : [yY];
fragment Z : [zZ];
/*--------------------------------------------------------------------------------*/